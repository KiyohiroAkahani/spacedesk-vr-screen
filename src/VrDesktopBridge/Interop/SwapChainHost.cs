using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace VrDesktopBridge.Interop;

/// <summary>
/// A WPF airspace host: creates a native child window (a lightweight
/// predefined "STATIC" window — no class registration / WndProc needed)
/// that the D3D11 swap chain presents into. WPF cannot share its own HWND
/// with a raw swap chain, so this child HWND provides a clean surface.
/// </summary>
public sealed class SwapChainHost : HwndHost
{
    private const int WS_CHILD = 0x40000000;
    private const int WS_VISIBLE = 0x10000000;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowExW(
        int dwExStyle, string lpClassName, string? lpWindowName, int dwStyle,
        int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(IntPtr hwnd);

    public IntPtr Hwnd { get; private set; }

    /// <summary>Raised once the child HWND exists (renderer init hook).</summary>
    public event Action<IntPtr>? HwndReady;

    protected override HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        Hwnd = CreateWindowExW(
            0, "STATIC", null,
            WS_CHILD | WS_VISIBLE,
            0, 0, 1, 1,
            hwndParent.Handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

        if (Hwnd == IntPtr.Zero)
            throw new InvalidOperationException(
                $"CreateWindowExW failed (Win32 error {Marshal.GetLastWin32Error()}).");

        HwndReady?.Invoke(Hwnd);
        return new HandleRef(this, Hwnd);
    }

    protected override void DestroyWindowCore(HandleRef hwnd)
    {
        if (Hwnd != IntPtr.Zero)
        {
            DestroyWindow(Hwnd);
            Hwnd = IntPtr.Zero;
        }
    }
}
