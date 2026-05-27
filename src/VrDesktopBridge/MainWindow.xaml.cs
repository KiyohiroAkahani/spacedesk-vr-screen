using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using VrDesktopBridge.Config;
using VrDesktopBridge.Interop;
using VrDesktopBridge.Monitors;
using VrDesktopBridge.Render;

namespace VrDesktopBridge;

/// <summary>
/// M2: borderless window placed over the TARGET monitor (the spacedesk
/// extended display), mirroring the PRIMARY monitor side-by-side.
/// Capture source (primary) ≠ display target (spacedesk) so there is no
/// hall-of-mirrors recursion. Hotkeys: Esc quit, F cycle target monitor.
/// </summary>
public partial class MainWindow : Window
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after,
        int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint affinity);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtrW(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtrW(IntPtr hWnd, int nIndex, IntPtr v);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ClipCursor(ref RECT rect);

    [DllImport("user32.dll", EntryPoint = "ClipCursor", SetLastError = true)]
    private static extern bool ClipCursorClear(IntPtr nullRect);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT p);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    private static readonly IntPtr HWND_TOP = IntPtr.Zero;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint WDA_NONE = 0x0;
    private const uint WDA_EXCLUDEFROMCAPTURE = 0x11;

    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TRANSPARENT = 0x00000020;
    private const long WS_EX_TOOLWINDOW = 0x00000080;
    private const long WS_EX_NOACTIVATE = 0x08000000;

    private const int WM_HOTKEY = 0x0312;
    private const int WM_DISPLAYCHANGE = 0x007E;
    private const int WM_NCHITTEST = 0x0084;
    private static readonly IntPtr HTTRANSPARENT = new(-1);
    private const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_SHIFT = 0x4;
    private const uint VK_Q = 0x51, VK_F = 0x46, VK_A = 0x41, VK_C = 0x43,
                       VK_D = 0x44, VK_H = 0x48, VK_V = 0x56, VK_S = 0x53,
                       VK_UP = 0x26, VK_DOWN = 0x28, VK_W = 0x57,
                       VK_I = 0x49, VK_O = 0x4F;
    private const int HK_QUIT = 1, HK_CYCLE = 2, HK_AFFINITY = 3,
                      HK_CLIP = 4, HK_REDETECT = 5, HK_HIDECURSOR = 6,
                      HK_DISTORT = 7, HK_SCALE = 8,
                      HK_SCALE_UP = 9, HK_SCALE_DOWN = 10, HK_WRANGLE = 11,
                      HK_IPD_IN = 12, HK_IPD_OUT = 13;
    private const float IpdMin = -0.15f, IpdMax = 0.15f, IpdStepAmt = 0.01f;
    private const float ScaleMin = 0.5f, ScaleMax = 1.5f, ScaleStepAmt = 0.1f;

    private static readonly float[] ScaleSteps = { 1.0f, 0.9f, 0.8f, 0.7f };
    private int _scaleIdx;

    private HwndSource? _hwndSource;
    private volatile bool _confineCursor;
    private MonitorInfo _captureMon = null!;
    private System.Windows.Threading.DispatcherTimer? _clipTimer;
    private System.Windows.Threading.DispatcherTimer? _guideTimer;
    private System.Windows.Threading.DispatcherTimer? _wrangleTimer;
    private readonly Interop.WindowWrangler _wrangler = new();
    private bool _keepWindowsOnCapture;
    // Independent of the render loop (which may be throttled when the
    // window is on a secondary/occluded display): a background timer that
    // hard-clamps the pointer every few ms.
    private System.Timers.Timer? _clampTimer;
    private long _frames;                       // diag: render ticks
    private long _clampCorrections;             // diag: SetCursorPos count
    private volatile string _lastCursorDiag = "n/a";

    private bool _excludeFromCapture;
    private readonly System.Diagnostics.Stopwatch _frameClock =
        System.Diagnostics.Stopwatch.StartNew();
    private double _lastFrameMs;

    private readonly AppConfig _config;
    private readonly SwapChainHost _host = new();
    private readonly SbsRenderer _renderer = new();
    private readonly Capture.CursorHider _cursorHider = new();
    private readonly Interop.GlobalEscHook _escHook = new();
    private bool _initialized;
    private bool _duplicateMode;          // single logical monitor (mirror)
    private bool _hideRealCursor;

    private IReadOnlyList<MonitorInfo> _monitors = Array.Empty<MonitorInfo>();
    private MonitorInfo _target = null!;

    public MainWindow()
    {
        InitializeComponent();
        _config = AppConfig.Load();
        _renderer.Mode = Render.RenderMode.SideBySide; // M2

        ResolveMonitors();
        // Single logical monitor => spacedesk duplicate/mirror (or no
        // extend). In that mode confinement is meaningless; instead we
        // hide the real OS cursor and rely on the composited one so the
        // user can actually aim/click.
        _duplicateMode = _monitors.Count <= 1;
        _confineCursor = _config.ConfineCursor && !_duplicateMode;
        _hideRealCursor = _duplicateMode;
        // Only meaningful with a separate (extended) monitor.
        _keepWindowsOnCapture = _config.KeepWindowsOnCapture && !_duplicateMode;
        WarnIfRecursionRisk();

        // Safety nets: never leave the OS cursor hidden.
        AppDomain.CurrentDomain.ProcessExit += (_, _) => _cursorHider.Restore();
        AppDomain.CurrentDomain.UnhandledException +=
            (_, _) => _cursorHider.Restore();

        HostGrid.Children.Add(_host);
        _host.HwndReady += OnHwndReady;

        SourceInitialized += (_, _) =>
        {
            SetupGlobalHotkeys();
            _escHook.Install(() => Dispatcher.BeginInvoke(new Action(QuitHard)));
            PlaceOnTarget();
            StartCursorConfinement();
            StartWindowWrangler();
        };
        SizeChanged += (_, _) => ResizeRenderer();
        KeyDown += OnKeyDown;
        Closed += OnClosed;
    }

    /// <summary>(Re)enumerate displays, dump them, and pick target+capture.</summary>
    private void ResolveMonitors()
    {
        _monitors = MonitorPicker.Enumerate();
        Console.Error.WriteLine($"[MON] {_monitors.Count} monitor(s):");
        for (int i = 0; i < _monitors.Count; i++)
            Console.Error.WriteLine($"[MON]  [{i}] {_monitors[i]}");

        _target = MonitorPicker.Resolve(_config.TargetMonitorMatch, _monitors);
        // Capture source is the primary monitor (DXGI output 0). Confining
        // the mouse there makes clicks land on the real mirrored windows.
        _captureMon = MonitorPicker.Resolve("primary", _monitors);
        _excludeFromCapture = _config.ExcludeFromCapture || _target.IsPrimary;
        Console.Error.WriteLine(
            $"[MON] target={_target}  capture={_captureMon}");
    }

    /// <summary>
    /// Re-detect displays and re-place the window. Fixes the case where
    /// spacedesk connects AFTER launch (one-shot enumeration bug) and
    /// keeps everything correct across display changes.
    /// </summary>
    private void ReDetect(string why)
    {
        Console.Error.WriteLine($"[INFO] Re-detecting monitors ({why})...");
        ResolveMonitors();
        WarnIfRecursionRisk();
        PlaceOnTarget();
        ResizeRenderer();
        if (_confineCursor) ApplyClip();
    }

    private void WarnIfRecursionRisk()
    {
        // Capture is the primary monitor (DXGI output 0). If the window is
        // also placed on primary, the capture will include this window =>
        // hall of mirrors. M3 wires WDA_EXCLUDEFROMCAPTURE; for now warn.
        if (_target.IsPrimary)
            Console.Error.WriteLine(
                "[WARN] Target monitor is PRIMARY — capture source == display " +
                "target. Expect hall-of-mirrors. Connect spacedesk in EXTEND " +
                "mode, or press F to move to another monitor.");
        Console.Error.WriteLine($"[INFO] Target monitor: {_target}");
    }

    private void PlaceOnTarget()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        ApplyClickThrough(hwnd);
        SetWindowPos(hwnd, HWND_TOP,
            _target.X, _target.Y, _target.Width, _target.Height,
            SWP_SHOWWINDOW | SWP_NOACTIVATE);
        ApplyAffinity();
    }

    /// <summary>
    /// Make the mirror window never steal focus and let the mouse fall
    /// through it (so working on the primary desktop is uninterrupted, and
    /// the cursor isn't trapped if it strays onto the spacedesk monitor).
    /// Note: WS_EX_LAYERED is intentionally NOT set — a layered top-level
    /// window cannot reliably host the live D3D child HWND (would blank the
    /// mirror). WS_EX_TRANSPARENT alone gives mouse hit-test pass-through.
    /// </summary>
    private void ApplyClickThrough(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;
        long ex = GetWindowLongPtrW(hwnd, GWL_EXSTYLE).ToInt64();
        ex |= WS_EX_NOACTIVATE | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW;
        SetWindowLongPtrW(hwnd, GWL_EXSTYLE, new IntPtr(ex));
    }

    private void SetupGlobalHotkeys()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        _hwndSource = HwndSource.FromHwnd(hwnd);
        _hwndSource?.AddHook(HotkeyHook);

        uint mods = MOD_CONTROL | MOD_ALT | MOD_SHIFT;
        bool ok =
            RegisterHotKey(hwnd, HK_QUIT, mods, VK_Q) &
            RegisterHotKey(hwnd, HK_CYCLE, mods, VK_F) &
            RegisterHotKey(hwnd, HK_AFFINITY, mods, VK_A) &
            RegisterHotKey(hwnd, HK_CLIP, mods, VK_C) &
            RegisterHotKey(hwnd, HK_REDETECT, mods, VK_D) &
            RegisterHotKey(hwnd, HK_HIDECURSOR, mods, VK_H) &
            RegisterHotKey(hwnd, HK_DISTORT, mods, VK_V) &
            RegisterHotKey(hwnd, HK_SCALE, mods, VK_S) &
            RegisterHotKey(hwnd, HK_SCALE_UP, mods, VK_UP) &
            RegisterHotKey(hwnd, HK_SCALE_DOWN, mods, VK_DOWN) &
            RegisterHotKey(hwnd, HK_WRANGLE, mods, VK_W) &
            RegisterHotKey(hwnd, HK_IPD_IN, mods, VK_I) &
            RegisterHotKey(hwnd, HK_IPD_OUT, mods, VK_O);
        Console.Error.WriteLine(ok
            ? "[INFO] Global hotkeys: Ctrl+Alt+Shift+ Q=quit F=cycle-monitor "
              + "A=capture-exclude C=confine-cursor D=re-detect-displays "
              + "H=hide-real-cursor V=vr-distortion S=scale-cycle "
              + "Up=enlarge Down=shrink W=keep-windows "
              + "I=ipd-narrower O=ipd-wider. (Terminal Ctrl+C quits.)"
            : $"[WARN] RegisterHotKey failed (Win32 {Marshal.GetLastWin32Error()}). "
              + "Use terminal Ctrl+C to quit.");
    }

    /// <summary>
    /// Clip the mouse to the captured (primary) monitor so that, while the
    /// user looks at the headset, every click lands on a real mirrored
    /// window instead of the empty spacedesk extended desktop. Windows
    /// drops the clip on focus / secure-desktop changes, so a timer keeps
    /// re-asserting it.
    /// </summary>
    private void StartWindowWrangler()
    {
        _wrangleTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(400),
        };
        _wrangleTimer.Tick += (_, _) =>
        {
            if (!_keepWindowsOnCapture) return;
            var own = new WindowInteropHelper(this).Handle;
            _wrangler.Tick(own, _captureMon.X, _captureMon.Y,
                _captureMon.Width, _captureMon.Height,
                m => Console.Error.WriteLine(m));
        };
        _wrangleTimer.Start();
        Console.Error.WriteLine(
            $"[INFO] KeepWindowsOnCapture={_keepWindowsOnCapture} "
            + "(stray windows pulled onto the mirrored monitor). "
            + "Toggle Ctrl+Alt+Shift+W.");
    }

    private void StartCursorConfinement()
    {
        ApplyClip();

        // Soft fence (re-assert ClipCursor) + 1 Hz diagnostics.
        _clipTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1000),
        };
        long lastFrames = 0;
        _clipTimer.Tick += (_, _) =>
        {
            if (_confineCursor) ApplyClip();
            long f = System.Threading.Interlocked.Read(ref _frames);
            long c = System.Threading.Interlocked.Read(ref _clampCorrections);
            Console.Error.WriteLine(
                $"[DIAG] renderFps={f - lastFrames} confine={_confineCursor} "
                + $"cap=({_captureMon.X},{_captureMon.Y},"
                + $"{_captureMon.Width}x{_captureMon.Height}) "
                + $"clampHits={c} cursor={_lastCursorDiag}");
            lastFrames = f;
        };
        _clipTimer.Start();

        // Hard clamp on a background thread — does NOT depend on WPF
        // rendering or window focus (GetCursorPos/SetCursorPos are global).
        _clampTimer = new System.Timers.Timer(8) { AutoReset = true };
        _clampTimer.Elapsed += (_, _) => ClampCursorToCapture();
        _clampTimer.Enabled = _confineCursor;

        Console.Error.WriteLine(
            $"[INFO] ConfineCursor={_confineCursor} on {_captureMon}. "
            + "Toggle with Ctrl+Alt+Shift+C.");
    }

    private void ApplyClip()
    {
        if (!_confineCursor) { ClipCursorClear(IntPtr.Zero); return; }
        var r = new RECT
        {
            Left = _captureMon.X,
            Top = _captureMon.Y,
            Right = _captureMon.X + _captureMon.Width,
            Bottom = _captureMon.Y + _captureMon.Height,
        };
        ClipCursor(ref r);
    }

    private const double GuideHoldMs = 7000;   // fully visible
    private const double GuideFadeMs = 900;    // then fade out
    private readonly System.Diagnostics.Stopwatch _guideClock =
        System.Diagnostics.Stopwatch.StartNew();
    private double _guideShownMs;

    /// <summary>
    /// Show the first-run guide and (re)arm the 7 s hold + fade-out.
    /// Called at startup and on every user action.
    /// </summary>
    private void BumpGuide()
    {
        _guideShownMs = _guideClock.Elapsed.TotalMilliseconds;
        _renderer.ShowGuide = true;
        _renderer.GuideOpacity = 1f;

        if (_guideTimer is null)
        {
            _guideTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(33), // ~30 fps fade
            };
            _guideTimer.Tick += (_, _) =>
            {
                double dt = _guideClock.Elapsed.TotalMilliseconds - _guideShownMs;
                if (dt < GuideHoldMs)
                {
                    _renderer.GuideOpacity = 1f;
                }
                else if (dt < GuideHoldMs + GuideFadeMs)
                {
                    _renderer.GuideOpacity =
                        (float)(1.0 - (dt - GuideHoldMs) / GuideFadeMs);
                }
                else
                {
                    _renderer.GuideOpacity = 0f;
                    _renderer.ShowGuide = false;
                    _guideTimer!.Stop();
                }
            };
        }
        _guideTimer.Stop();
        _guideTimer.Start();
    }

    private void ToggleConfine()
    {
        _confineCursor = !_confineCursor;
        if (_clampTimer is not null) _clampTimer.Enabled = _confineCursor;
        if (_confineCursor) ApplyClip();
        else ClipCursorClear(IntPtr.Zero);
        Console.Error.WriteLine($"[INFO] ConfineCursor={_confineCursor}");
    }

    private IntPtr HotkeyHook(IntPtr hwnd, int msg, IntPtr wParam,
                              IntPtr lParam, ref bool handled)
    {
        if (msg == WM_NCHITTEST)
        {
            // Make the whole mirror window click-through: every mouse hit
            // is reported as "transparent" so the click falls to the real
            // window beneath. WS_EX_TRANSPARENT alone is unreliable without
            // WS_EX_LAYERED (which would blank the D3D child), so we do the
            // hit-test ourselves. Without this, in duplicate mode the
            // fullscreen window swallows every click.
            handled = true;
            return HTTRANSPARENT;
        }
        if (msg == WM_DISPLAYCHANGE)
        {
            // Display added/removed/resized (e.g. spacedesk connected).
            ReDetect("WM_DISPLAYCHANGE");
            return IntPtr.Zero;
        }
        if (msg != WM_HOTKEY) return IntPtr.Zero;
        BumpGuide(); // any hotkey counts as activity
        switch (wParam.ToInt32())
        {
            case HK_QUIT:
                handled = true;
                QuitHard();
                break;
            case HK_REDETECT:
                handled = true;
                ReDetect("hotkey");
                break;
            case HK_CYCLE:
                handled = true;
                CycleTargetMonitor();
                break;
            case HK_AFFINITY:
                handled = true;
                _excludeFromCapture = !_excludeFromCapture;
                ApplyAffinity();
                break;
            case HK_CLIP:
                handled = true;
                ToggleConfine();
                break;
            case HK_HIDECURSOR:
                handled = true;
                _hideRealCursor = !_hideRealCursor;
                ApplyCursorHide();
                break;
            case HK_DISTORT:
                handled = true;
                _renderer.DistortionEnabled = !_renderer.DistortionEnabled;
                Console.Error.WriteLine(
                    $"[INFO] LensDistortion={_renderer.DistortionEnabled}");
                break;
            case HK_SCALE:
                handled = true;
                CycleScale();
                break;
            case HK_SCALE_UP:
                handled = true;
                ScaleBy(+ScaleStepAmt);
                break;
            case HK_SCALE_DOWN:
                handled = true;
                ScaleBy(-ScaleStepAmt);
                break;
            case HK_WRANGLE:
                handled = true;
                _keepWindowsOnCapture = !_keepWindowsOnCapture;
                Console.Error.WriteLine(
                    $"[INFO] KeepWindowsOnCapture={_keepWindowsOnCapture}");
                break;
            case HK_IPD_IN:
                handled = true;
                ShiftIpdBy(+IpdStepAmt);
                break;
            case HK_IPD_OUT:
                handled = true;
                ShiftIpdBy(-IpdStepAmt);
                break;
        }
        return IntPtr.Zero;
    }

    /// <summary>Step the per-eye IPD shift, clamped to [-15%, +15%].</summary>
    private void ShiftIpdBy(float d)
    {
        float v = _renderer.IpdShift + d;
        v = v < IpdMin ? IpdMin : (v > IpdMax ? IpdMax : v);
        v = (float)Math.Round(v, 2);
        _renderer.IpdShift = v;
        Console.Error.WriteLine(
            $"[INFO] IpdShift={(int)Math.Round(v * 100)}%");
    }

    /// <summary>Step the SBS scale by <paramref name="d"/>, clamped.</summary>
    private void ScaleBy(float d)
    {
        float v = _renderer.ContentScale + d;
        v = v < ScaleMin ? ScaleMin : (v > ScaleMax ? ScaleMax : v);
        v = (float)Math.Round(v, 2);
        _renderer.ContentScale = v;
        _scaleIdx = -1; // decouple from the preset cycle
        Console.Error.WriteLine($"[INFO] ScreenScale={(int)(v * 100)}%");
    }

    /// <summary>Step the SBS image scale 100→90→80→70→100… (loops).</summary>
    private void CycleScale()
    {
        _scaleIdx = (_scaleIdx + 1) % ScaleSteps.Length;
        _renderer.ContentScale = ScaleSteps[_scaleIdx];
        Console.Error.WriteLine(
            $"[INFO] ScreenScale={(int)(ScaleSteps[_scaleIdx] * 100)}%");
    }

    /// <summary>
    /// Hide/show the real OS cursor and switch the composited cursor to a
    /// fixed arrow while hidden (the live shot would be invisible).
    /// </summary>
    private void ApplyCursorHide()
    {
        _renderer.UseStandardArrow = _hideRealCursor;
        if (_hideRealCursor) _cursorHider.Hide();
        else _cursorHider.Restore();
        Console.Error.WriteLine($"[INFO] HideRealCursor={_hideRealCursor}");
    }

    private void ApplyAffinity()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        bool ok = SetWindowDisplayAffinity(hwnd,
            _excludeFromCapture ? WDA_EXCLUDEFROMCAPTURE : WDA_NONE);
        Console.Error.WriteLine(
            $"[INFO] ExcludeFromCapture={_excludeFromCapture} " +
            (ok ? "applied" : $"FAILED (Win32 {Marshal.GetLastWin32Error()})"));
    }

    private (int w, int h) PixelSize() => (_target.Width, _target.Height);

    private void OnHwndReady(IntPtr hwnd)
    {
        try
        {
            var (w, h) = PixelSize();
            _renderer.Initialize(hwnd, w, h);
            _renderer.DistortionEnabled = _config.LensDistortion;
            _renderer.K1 = _config.DistortK1;
            _renderer.K2 = _config.DistortK2;
            _renderer.ContentScale = _config.StartupScale;
            _scaleIdx = -1; // up/down steps from the startup scale
            Console.Error.WriteLine(
                $"[INFO] LensDistortion={_config.LensDistortion} "
                + $"k1={_config.DistortK1} k2={_config.DistortK2}. "
                + "Toggle with Ctrl+Alt+Shift+V.");
            Console.Error.WriteLine(
                $"[INFO] ScreenScale={(int)(_config.StartupScale * 100)}% "
                + "(startup). Ctrl+Alt+Shift+Up/Down to change.");
            _renderer.IpdShift = _config.IpdShift;
            Console.Error.WriteLine(
                $"[INFO] IpdShift={(int)Math.Round(_renderer.IpdShift * 100)}% "
                + "(startup). Ctrl+Alt+Shift+I/O = narrower/wider.");
            _initialized = true;
            // Duplicate/mirror mode: hide the confusing real cursor and
            // draw a fixed arrow at the (content-correct) composited spot.
            ApplyCursorHide();
            BumpGuide(); // show the first-run guide; hides after 3 s idle
            CompositionTarget.Rendering += OnRendering;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Renderer init failed:\n{ex}", "VrDesktopBridge",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }
    }

    private void ResizeRenderer()
    {
        if (!_initialized) return;
        var (w, h) = PixelSize();
        _renderer.Resize(w, h);
    }

    /// <summary>
    /// Hard cursor clamp to the captured (primary) monitor, every tick.
    /// ClipCursor is unreliable for a non-foreground window (the OS keeps
    /// releasing it), so we additionally force the pointer back with
    /// SetCursorPos — this works regardless of focus. Without this the
    /// real pointer roams onto the empty spacedesk display (the user sees
    /// a 3rd cursor there) and clicks hit nothing.
    /// </summary>
    private void ClampCursorToCapture()
    {
        if (!_confineCursor) return;
        if (!GetCursorPos(out POINT p)) return;

        int left = _captureMon.X, top = _captureMon.Y;
        int right = left + _captureMon.Width - 1;
        int bottom = top + _captureMon.Height - 1;

        int nx = p.X < left ? left : (p.X > right ? right : p.X);
        int ny = p.Y < top ? top : (p.Y > bottom ? bottom : p.Y);
        bool corrected = nx != p.X || ny != p.Y;
        if (corrected)
        {
            SetCursorPos(nx, ny);
            System.Threading.Interlocked.Increment(ref _clampCorrections);
        }
        _lastCursorDiag = $"({p.X},{p.Y})->({nx},{ny}){(corrected ? "*" : "")}";
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (!_initialized) return;

        System.Threading.Interlocked.Increment(ref _frames);

        // Also clamp from the render tick (belt & suspenders; the primary
        // driver is the background _clampTimer).
        ClampCursorToCapture();

        // FPS cap (Present already vsyncs; this throttles below refresh).
        double now = _frameClock.Elapsed.TotalMilliseconds;
        double minInterval = 1000.0 / _config.ClampedFps;
        if (now - _lastFrameMs < minInterval) return;
        _lastFrameMs = now;

        try
        {
            _renderer.RenderFrame();
        }
        catch (Exception ex)
        {
            CompositionTarget.Rendering -= OnRendering;
            MessageBox.Show($"Render loop stopped:\n{ex}", "VrDesktopBridge",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CycleTargetMonitor()
    {
        if (_monitors.Count < 2) return;
        int i = _monitors.ToList().FindIndex(m => m.DeviceName == _target.DeviceName);
        _target = _monitors[(i + 1) % _monitors.Count];
        _excludeFromCapture = _config.ExcludeFromCapture || _target.IsPrimary;
        Console.Error.WriteLine($"[INFO] Switched target -> {_target}");
        PlaceOnTarget();
        ResizeRenderer();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        BumpGuide(); // key activity keeps the guide alive
        switch (e.Key)
        {
            case Key.Escape:
                QuitHard();
                break;
            case Key.F:
                CycleTargetMonitor();
                break;
            case Key.A:
                _excludeFromCapture = !_excludeFromCapture;
                ApplyAffinity();
                break;
            case Key.C:
                ToggleConfine();
                break;
            case Key.D:
                ReDetect("key");
                break;
            case Key.H:
                _hideRealCursor = !_hideRealCursor;
                ApplyCursorHide();
                break;
            case Key.V:
                _renderer.DistortionEnabled = !_renderer.DistortionEnabled;
                Console.Error.WriteLine(
                    $"[INFO] LensDistortion={_renderer.DistortionEnabled}");
                break;
            case Key.S:
                CycleScale();
                break;
            case Key.Up:
                ScaleBy(+ScaleStepAmt);
                break;
            case Key.Down:
                ScaleBy(-ScaleStepAmt);
                break;
            case Key.W:
                _keepWindowsOnCapture = !_keepWindowsOnCapture;
                Console.Error.WriteLine(
                    $"[INFO] KeepWindowsOnCapture={_keepWindowsOnCapture}");
                break;
            case Key.I:
                ShiftIpdBy(+IpdStepAmt);
                break;
            case Key.O:
                ShiftIpdBy(-IpdStepAmt);
                break;
        }
    }

    private bool _quitting;

    /// <summary>
    /// Fully terminate the program (parity with terminal Ctrl+C). Runs the
    /// normal teardown (cursor/clip restore, hook uninstall, hotkey
    /// unregister, renderer dispose via OnClosed) then forces the process
    /// to exit so nothing (timers, hooks, dotnet run) can keep it alive.
    /// </summary>
    private void QuitHard()
    {
        if (_quitting) return;
        _quitting = true;
        try { Close(); } catch { /* ensure exit regardless */ }
        Environment.Exit(0);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        CompositionTarget.Rendering -= OnRendering;
        _clipTimer?.Stop();
        _clipTimer = null;
        _guideTimer?.Stop();
        _guideTimer = null;
        _wrangleTimer?.Stop();
        _wrangleTimer = null;
        if (_clampTimer is not null)
        {
            _clampTimer.Stop();
            _clampTimer.Dispose();
            _clampTimer = null;
        }
        _escHook.Uninstall();
        ClipCursorClear(IntPtr.Zero); // never leave the mouse trapped
        _cursorHider.Restore();       // never leave the OS cursor hidden
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
        {
            UnregisterHotKey(hwnd, HK_QUIT);
            UnregisterHotKey(hwnd, HK_CYCLE);
            UnregisterHotKey(hwnd, HK_AFFINITY);
            UnregisterHotKey(hwnd, HK_CLIP);
            UnregisterHotKey(hwnd, HK_REDETECT);
            UnregisterHotKey(hwnd, HK_HIDECURSOR);
            UnregisterHotKey(hwnd, HK_DISTORT);
            UnregisterHotKey(hwnd, HK_SCALE);
            UnregisterHotKey(hwnd, HK_SCALE_UP);
            UnregisterHotKey(hwnd, HK_SCALE_DOWN);
            UnregisterHotKey(hwnd, HK_WRANGLE);
            UnregisterHotKey(hwnd, HK_IPD_IN);
            UnregisterHotKey(hwnd, HK_IPD_OUT);
        }
        _hwndSource?.RemoveHook(HotkeyHook);
        _hwndSource = null;
        _renderer.Dispose();
    }
}
