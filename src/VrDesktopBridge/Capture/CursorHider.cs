using System.Runtime.InteropServices;

namespace VrDesktopBridge.Capture;

/// <summary>
/// Hides the real system mouse cursor globally (duplicate/mirror mode).
///
/// Why: in spacedesk DUPLICATE mode the iPhone mirrors the primary
/// framebuffer, which includes the hardware cursor drawn at UN-scaled
/// screen coords. In the side-by-side view that real cursor never lines
/// up with the scaled content, so the user can't aim. We hide it and
/// rely on the renderer's composited cursor (which IS drawn at the
/// content-correct, scaled position). Clicks still go to the real
/// pointer position, which matches because the same transform is used.
///
/// Implemented with SetSystemCursor (transparent) for every standard
/// cursor id; Restore() reloads the user's real cursors. ALWAYS restore
/// on every exit path — see also tools/stop_app.py / run_app.py which
/// reset cursors as a safety net after a force-kill.
/// </summary>
public sealed class CursorHider
{
    private const uint SPI_SETCURSORS = 0x0057;

    private static readonly uint[] Ocr =
    {
        32512, // OCR_NORMAL
        32513, // OCR_IBEAM
        32514, // OCR_WAIT
        32515, // OCR_CROSS
        32516, // OCR_UP
        32642, // OCR_SIZENWSE
        32643, // OCR_SIZENESW
        32644, // OCR_SIZEWE
        32645, // OCR_SIZENS
        32646, // OCR_SIZEALL
        32648, // OCR_NO
        32649, // OCR_HAND
        32650, // OCR_APPSTARTING
    };

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreateCursor(IntPtr hInst, int xHot, int yHot,
        int nWidth, int nHeight, byte[] pvANDPlane, byte[] pvXORPlane);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetSystemCursor(IntPtr hcur, uint id);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CopyIcon(IntPtr hIcon);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfoW(uint uiAction, uint uiParam,
        IntPtr pvParam, uint fWinIni);

    public bool IsHidden { get; private set; }

    public void Hide()
    {
        if (IsHidden) return;
        // 32x32 fully-transparent cursor: AND=all 1 (keep background),
        // XOR=all 0 (don't invert) => nothing drawn.
        var and = new byte[32 * 32 / 8];
        var xor = new byte[32 * 32 / 8];
        for (int i = 0; i < and.Length; i++) and[i] = 0xFF;

        IntPtr blank = CreateCursor(IntPtr.Zero, 0, 0, 32, 32, and, xor);
        if (blank == IntPtr.Zero) return;

        foreach (uint id in Ocr)
        {
            // SetSystemCursor destroys the handle it's given — pass a copy.
            IntPtr copy = CopyIcon(blank);
            if (copy != IntPtr.Zero)
                SetSystemCursor(copy, id);
        }
        IsHidden = true;
    }

    public void Restore()
    {
        if (!IsHidden) return;
        // Reload the user's configured cursors from the registry.
        SystemParametersInfoW(SPI_SETCURSORS, 0, IntPtr.Zero, 0);
        IsHidden = false;
    }
}
