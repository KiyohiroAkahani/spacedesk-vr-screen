using System.Runtime.InteropServices;
using System.Text;

namespace VrDesktopBridge.Interop;

/// <summary>
/// EXTEND-mode safety net. We mirror the CAPTURED (primary) monitor, but
/// Windows can open/restore app windows onto the spacedesk extended
/// monitor — where they are invisible (covered by our mirror) and
/// unreachable (the cursor is confined to primary). This watches the
/// foreground window and, if it has strayed off the captured monitor,
/// moves/resizes it back so it is always visible and usable in VR.
/// </summary>
public sealed class WindowWrangler
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr h, StringBuilder s, int max);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr h, IntPtr after,
        int x, int y, int cx, int cy, uint flags);

    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    // Shell / system windows we must never move.
    private static readonly HashSet<string> Skip = new(StringComparer.Ordinal)
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd",
        "Windows.UI.Core.CoreWindow", "ForegroundStaging",
        "Microsoft.UI.Content.DesktopChildSiteBridge",
    };

    private IntPtr _lastHandled;

    /// <summary>
    /// Check the foreground window; if it is a normal window whose centre
    /// is NOT on the captured monitor, move it fully onto it.
    /// </summary>
    public void Tick(IntPtr ownHwnd, int capX, int capY, int capW, int capH,
                     Action<string> log)
    {
        IntPtr h = GetForegroundWindow();
        if (h == IntPtr.Zero || h == ownHwnd) return;
        if (!IsWindowVisible(h) || IsIconic(h)) return;

        var sb = new StringBuilder(64);
        GetClassName(h, sb, sb.Capacity);
        if (Skip.Contains(sb.ToString())) return;

        if (!GetWindowRect(h, out RECT r)) return;
        int w = r.Right - r.Left, hgt = r.Bottom - r.Top;
        if (w <= 0 || hgt <= 0) return;

        int cx = r.Left + w / 2, cy = r.Top + hgt / 2;
        bool onCapture = cx >= capX && cx < capX + capW &&
                         cy >= capY && cy < capY + capH;
        if (onCapture) { _lastHandled = h; return; }

        if (h == _lastHandled) return; // already tried; don't fight in a loop
        _lastHandled = h;

        // Fit within the captured monitor (small margin), centred.
        int nw = Math.Min(w, capW - 40);
        int nh = Math.Min(hgt, capH - 40);
        nw = Math.Max(nw, 200);
        nh = Math.Max(nh, 150);
        int nx = capX + (capW - nw) / 2;
        int ny = capY + (capH - nh) / 2;
        SetWindowPos(h, IntPtr.Zero, nx, ny, nw, nh,
            SWP_NOZORDER | SWP_NOACTIVATE);
        log($"[INFO] Moved stray window ({sb}) onto captured monitor "
            + $"({nx},{ny} {nw}x{nh}).");
    }
}
