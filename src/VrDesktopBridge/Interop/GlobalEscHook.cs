using System.Diagnostics;
using System.Runtime.InteropServices;

namespace VrDesktopBridge.Interop;

/// <summary>
/// User-friendly quit: a global low-level keyboard hook that fires when
/// Esc is pressed TWICE quickly (double-tap).
///
/// Why double-tap + pass-through: the mirror window is click-through and
/// never focused, so a normal WPF KeyDown never sees Esc. Making Esc a
/// global hotkey would hijack Esc for every other app. A low-level hook
/// that observes (but does NOT swallow) Esc lets single-Esc keep working
/// everywhere, while a quick double-Esc cleanly exits VR — unlikely to be
/// triggered by accident.
/// </summary>
public sealed class GlobalEscHook
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int VK_ESCAPE = 0x1B;
    private const long DoubleTapMs = 500;

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookExW(int idHook, HookProc lpfn,
        IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
        IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string? lpModuleName);

    private HookProc? _proc;          // keep alive (no GC)
    private IntPtr _hook = IntPtr.Zero;
    private long _lastEscMs = -10000;
    private Action? _onDoubleEsc;

    /// <summary>Install the hook. Call on the UI thread (it pumps messages).</summary>
    public void Install(Action onDoubleEsc)
    {
        if (_hook != IntPtr.Zero) return;
        _onDoubleEsc = onDoubleEsc;
        _proc = Callback;
        _hook = SetWindowsHookExW(WH_KEYBOARD_LL, _proc,
            GetModuleHandleW(null), 0);
        if (_hook == IntPtr.Zero)
            Console.Error.WriteLine(
                $"[WARN] Esc hook failed (Win32 {Marshal.GetLastWin32Error()}). "
                + "Use Ctrl+Alt+Shift+Q or stop_app.py to quit.");
        else
            Console.Error.WriteLine(
                "[INFO] Quit: tap Esc twice quickly (within 0.5s).");
    }

    public void Uninstall()
    {
        if (_hook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
        _proc = null;
    }

    private IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
            {
                var k = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                if (k.vkCode == VK_ESCAPE)
                {
                    long now = Stopwatch.GetTimestamp() * 1000
                               / Stopwatch.Frequency;
                    if (now - _lastEscMs <= DoubleTapMs)
                    {
                        _lastEscMs = -10000;          // require fresh pair
                        _onDoubleEsc?.Invoke();
                    }
                    else
                    {
                        _lastEscMs = now;
                    }
                }
            }
        }
        // Never swallow the key — single Esc must still work everywhere.
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }
}
