using System.Runtime.InteropServices;

namespace VrDesktopBridge.Capture;

/// <summary>
/// Snapshots the current system mouse cursor (DXGI Desktop Duplication
/// does NOT composite the pointer, so without this the mirror shows no
/// cursor and the desktop is unusable through the headset).
///
/// Produces a straight-alpha BGRA top-down bitmap + hotspot + screen
/// position. The bitmap is rebuilt only when the cursor handle changes
/// (tracked by <see cref="ShapeVersion"/>); position updates every call.
/// Handles both 32bpp colour cursors (with/without embedded alpha) and
/// legacy monochrome (AND/XOR) cursors.
/// </summary>
public sealed class CursorOverlay
{
    private const int CURSOR_SHOWING = 0x0001;
    private const uint DIB_RGB_COLORS = 0;
    private const uint BI_RGB = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct CURSORINFO
    {
        public int cbSize;
        public int flags;
        public IntPtr hCursor;
        public POINT ptScreenPos;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ICONINFO
    {
        public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAP
    {
        public int bmType, bmWidth, bmHeight, bmWidthBytes;
        public ushort bmPlanes, bmBitsPixel;
        public IntPtr bmBits;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public int biSize, biWidth, biHeight;
        public ushort biPlanes, biBitCount;
        public uint biCompression, biSizeImage;
        public int biXPelsPerMeter, biYPelsPerMeter;
        public uint biClrUsed, biClrImportant;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorInfo(ref CURSORINFO pci);
    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadCursorW(IntPtr hInstance, int lpCursorName);
    [DllImport("user32.dll")]
    private static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);
    [DllImport("gdi32.dll")]
    private static extern int GetObject(IntPtr h, int c, ref BITMAP pv);
    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr ho);
    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr hdc, IntPtr hbm, uint start,
        uint cLines, byte[]? bits, ref BITMAPINFOHEADER bmi, uint usage);

    public bool Visible { get; private set; }
    public int ScreenX { get; private set; }
    public int ScreenY { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public int HotspotX { get; private set; }
    public int HotspotY { get; private set; }
    public byte[] Pixels { get; private set; } = Array.Empty<byte>();

    /// <summary>Increments whenever the cursor SHAPE changes.</summary>
    public int ShapeVersion { get; private set; }

    /// <summary>
    /// When true (duplicate-mode: the real OS cursor is hidden so it
    /// doesn't confuse aiming), always draw a fixed standard arrow and
    /// only track position — never snapshot the (now invisible) live
    /// system cursor.
    /// </summary>
    public bool ForceStandardArrow { get; set; }

    private IntPtr _lastCursor = IntPtr.Zero;
    private bool _stdArrowBuilt;

    /// <summary>Refresh visibility/position; rebuild bitmap if shape changed.</summary>
    public void Update()
    {
        if (ForceStandardArrow)
        {
            // Position from GetCursorPos (works even if the OS cursor was
            // hidden via SetSystemCursor); shape is a fixed arrow.
            if (GetCursorPos(out POINT cp))
            {
                Visible = true;
                ScreenX = cp.X;
                ScreenY = cp.Y;
            }
            if (!_stdArrowBuilt)
            {
                IntPtr arrow = LoadCursorW(IntPtr.Zero, 32512 /*IDC_ARROW*/);
                if (arrow != IntPtr.Zero && BuildBitmap(arrow))
                {
                    _stdArrowBuilt = true;
                    ShapeVersion++;
                }
            }
            return;
        }

        var ci = new CURSORINFO { cbSize = Marshal.SizeOf<CURSORINFO>() };
        if (!GetCursorInfo(ref ci) || (ci.flags & CURSOR_SHOWING) == 0
            || ci.hCursor == IntPtr.Zero)
        {
            Visible = false;
            return;
        }

        Visible = true;
        ScreenX = ci.ptScreenPos.X;
        ScreenY = ci.ptScreenPos.Y;

        if (ci.hCursor == _lastCursor && Pixels.Length > 0)
            return; // same shape — position already updated

        try
        {
            if (BuildBitmap(ci.hCursor))
            {
                _lastCursor = ci.hCursor;
                ShapeVersion++;
            }
        }
        catch
        {
            // Best-effort: keep last shape if extraction fails.
        }
    }

    private static byte[]? GetBitmap32(IntPtr hdc, IntPtr hbm, int w, int h)
    {
        var bmi = new BITMAPINFOHEADER
        {
            biSize = Marshal.SizeOf<BITMAPINFOHEADER>(),
            biWidth = w,
            biHeight = -h,            // negative => top-down
            biPlanes = 1,
            biBitCount = 32,
            biCompression = BI_RGB,
        };
        var buf = new byte[w * h * 4];
        int scan = GetDIBits(hdc, hbm, 0, (uint)h, buf, ref bmi, DIB_RGB_COLORS);
        return scan == 0 ? null : buf;
    }

    private bool BuildBitmap(IntPtr hCursor)
    {
        if (!GetIconInfo(hCursor, out ICONINFO ii))
            return false;

        IntPtr hdc = CreateCompatibleDC(IntPtr.Zero);
        try
        {
            var bm = new BITMAP();
            if (ii.hbmColor != IntPtr.Zero)
            {
                // Colour cursor: hbmColor is WxH 32bpp; hbmMask is WxH AND.
                GetObject(ii.hbmColor, Marshal.SizeOf<BITMAP>(), ref bm);
                int w = bm.bmWidth, h = bm.bmHeight;
                var color = GetBitmap32(hdc, ii.hbmColor, w, h);
                var mask = GetBitmap32(hdc, ii.hbmMask, w, h);
                if (color is null) return false;

                bool hasAlpha = false;
                for (int i = 3; i < color.Length; i += 4)
                    if (color[i] != 0) { hasAlpha = true; break; }

                var px = new byte[w * h * 4];
                for (int i = 0; i < w * h; i++)
                {
                    int o = i * 4;
                    px[o + 0] = color[o + 0]; // B
                    px[o + 1] = color[o + 1]; // G
                    px[o + 2] = color[o + 2]; // R
                    if (hasAlpha)
                        px[o + 3] = color[o + 3];
                    else
                        // AND mask: black(0)=opaque, white(!=0)=transparent
                        px[o + 3] = (byte)((mask != null && mask[o] != 0) ? 0 : 255);
                }
                Commit(px, w, h, ii.xHotspot, ii.yHotspot);
                return true;
            }
            else
            {
                // Monochrome: hbmMask is W x (2H): top=AND, bottom=XOR.
                GetObject(ii.hbmMask, Marshal.SizeOf<BITMAP>(), ref bm);
                int w = bm.bmWidth, h2 = bm.bmHeight, h = h2 / 2;
                var m = GetBitmap32(hdc, ii.hbmMask, w, h2);
                if (m is null || h == 0) return false;

                var px = new byte[w * h * 4];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        bool and = m[(y * w + x) * 4] != 0;
                        bool xor = m[((y + h) * w + x) * 4] != 0;
                        int o = (y * w + x) * 4;
                        byte b, g, r, a;
                        if (and && !xor) { b = g = r = 0; a = 0; }       // transparent
                        else if (!and && !xor) { b = g = r = 0; a = 255; } // black
                        else { b = g = r = 255; a = 255; }                 // white/invert
                        px[o] = b; px[o + 1] = g; px[o + 2] = r; px[o + 3] = a;
                    }
                Commit(px, w, h, ii.xHotspot, ii.yHotspot);
                return true;
            }
        }
        finally
        {
            if (ii.hbmColor != IntPtr.Zero) DeleteObject(ii.hbmColor);
            if (ii.hbmMask != IntPtr.Zero) DeleteObject(ii.hbmMask);
            DeleteDC(hdc);
        }
    }

    private void Commit(byte[] px, int w, int h, int hotX, int hotY)
    {
        Pixels = px;
        Width = w;
        Height = h;
        HotspotX = hotX;
        HotspotY = hotY;
    }
}
