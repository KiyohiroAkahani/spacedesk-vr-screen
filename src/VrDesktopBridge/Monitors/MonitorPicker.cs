using System.Runtime.InteropServices;

namespace VrDesktopBridge.Monitors;

/// <summary>One display: pixel bounds + primary flag + friendly name.</summary>
public sealed record MonitorInfo(
    string DeviceName,    // e.g. \\.\DISPLAY1
    string FriendlyName,  // adapter DeviceString, e.g. "spacedesk Graphics Adapter"
    int X, int Y, int Width, int Height,
    bool IsPrimary)
{
    public bool IsLikelySpacedesk =>
        FriendlyName.Contains("spacedesk", StringComparison.OrdinalIgnoreCase) ||
        FriendlyName.Contains("datronicsoft", StringComparison.OrdinalIgnoreCase);

    public override string ToString() =>
        $"{DeviceName} \"{FriendlyName}\" {Width}x{Height}@({X},{Y})" +
        (IsPrimary ? " [primary]" : "") +
        (IsLikelySpacedesk ? " [spacedesk]" : "");
}

/// <summary>
/// Enumerates monitors via Win32 and resolves a config match string
/// ("primary" | "spacedesk" | substring | numeric index) to a monitor.
/// </summary>
public static class MonitorPicker
{
    private const int MONITORINFOF_PRIMARY = 0x1;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MONITORINFOEXW
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DISPLAY_DEVICEW
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
        public int StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
    }

    private delegate bool MonitorEnumProc(IntPtr hMon, IntPtr hdc, ref RECT rc, IntPtr data);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(
        IntPtr hdc, IntPtr clip, MonitorEnumProc proc, IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfoW(IntPtr hMon, ref MONITORINFOEXW mi);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplayDevicesW(
        string? device, uint devNum, ref DISPLAY_DEVICEW dd, uint flags);

    public static IReadOnlyList<MonitorInfo> Enumerate()
    {
        var list = new List<MonitorInfo>();

        bool Callback(IntPtr hMon, IntPtr hdc, ref RECT rc, IntPtr data)
        {
            var mi = new MONITORINFOEXW { cbSize = Marshal.SizeOf<MONITORINFOEXW>() };
            if (!GetMonitorInfoW(hMon, ref mi))
                return true;

            string friendly = mi.szDevice;
            var dd = new DISPLAY_DEVICEW { cb = Marshal.SizeOf<DISPLAY_DEVICEW>() };
            if (EnumDisplayDevicesW(mi.szDevice, 0, ref dd, 0))
                friendly = dd.DeviceString;

            list.Add(new MonitorInfo(
                mi.szDevice, friendly,
                mi.rcMonitor.Left, mi.rcMonitor.Top,
                mi.rcMonitor.Right - mi.rcMonitor.Left,
                mi.rcMonitor.Bottom - mi.rcMonitor.Top,
                (mi.dwFlags & MONITORINFOF_PRIMARY) != 0));
            return true;
        }

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, Callback, IntPtr.Zero);
        return list;
    }

    /// <summary>
    /// Resolve a match string. Order: numeric index → "primary" →
    /// "spacedesk" (heuristic, else first non-primary) → name substring.
    /// Always returns something (falls back to primary, then first).
    /// </summary>
    public static MonitorInfo Resolve(string match, IReadOnlyList<MonitorInfo>? monitors = null)
    {
        var mons = monitors ?? Enumerate();
        if (mons.Count == 0)
            throw new InvalidOperationException("No monitors enumerated.");

        MonitorInfo Primary() => mons.FirstOrDefault(m => m.IsPrimary) ?? mons[0];

        if (int.TryParse(match, out int idx) && idx >= 0 && idx < mons.Count)
            return mons[idx];

        if (match.Equals("primary", StringComparison.OrdinalIgnoreCase))
            return Primary();

        if (match.Equals("spacedesk", StringComparison.OrdinalIgnoreCase))
            return mons.FirstOrDefault(m => m.IsLikelySpacedesk)
                ?? mons.FirstOrDefault(m => !m.IsPrimary)
                ?? Primary();

        return mons.FirstOrDefault(m =>
                   m.FriendlyName.Contains(match, StringComparison.OrdinalIgnoreCase) ||
                   m.DeviceName.Contains(match, StringComparison.OrdinalIgnoreCase))
               ?? Primary();
    }
}
