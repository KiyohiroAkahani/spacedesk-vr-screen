using System.Diagnostics;
using SharpGen.Runtime;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace VrDesktopBridge.Capture;

/// <summary>
/// Captures one display via the DXGI Desktop Duplication API.
///
/// Chosen over Windows.Graphics.Capture for v1: single-GPU full-monitor
/// mirror needs none of WGC's cross-GPU / per-window features, DXGI
/// Duplication has NO yellow capture border, and it avoids the brittle
/// WinRT IGraphicsCaptureItemInterop COM dance. Trade-off: capture device
/// must be on the same GPU as the output (true here — one machine).
/// </summary>
public sealed class DesktopDuplicator : IDisposable
{
    private readonly ID3D11Device _device;
    private IDXGIOutputDuplication? _dupl;
    private IDXGIOutput1? _output1;

    public int OutputWidth { get; private set; }
    public int OutputHeight { get; private set; }
    public int OutputX { get; private set; }   // virtual-desktop origin of
    public int OutputY { get; private set; }   // the captured monitor
    public Format OutputFormat { get; private set; } = Format.B8G8R8A8_UNorm;

    /// <param name="device">Shared D3D11 device (same one the renderer uses).</param>
    /// <param name="outputIndex">Adapter output index (0 = primary).</param>
    public DesktopDuplicator(ID3D11Device device, int outputIndex = 0)
    {
        _device = device;
        Initialize(outputIndex);
    }

    private void Initialize(int outputIndex)
    {
        using var dxgiDevice = _device.QueryInterface<IDXGIDevice>();
        using var adapter = dxgiDevice.GetAdapter();
        adapter.EnumOutputs((uint)outputIndex, out IDXGIOutput? output).CheckError();
        if (output is null)
            throw new InvalidOperationException(
                $"No DXGI output at index {outputIndex}.");
        _output1 = output.QueryInterface<IDXGIOutput1>();
        output.Dispose();

        var desc = _output1.Description;
        var bounds = desc.DesktopCoordinates;
        OutputX = bounds.Left;
        OutputY = bounds.Top;
        OutputWidth = bounds.Right - bounds.Left;
        OutputHeight = bounds.Bottom - bounds.Top;

        _dupl = _output1.DuplicateOutput(_device);
    }

    /// <summary>
    /// Try to grab the latest desktop frame and copy it into
    /// <paramref name="dest"/>. Returns false if no new frame was ready
    /// within the timeout (caller should reuse the previous frame).
    /// </summary>
    private readonly System.Diagnostics.Stopwatch _recreateClock =
        System.Diagnostics.Stopwatch.StartNew();
    private double _lastRecreateMs = -10000;

    public bool TryCopyFrameTo(ID3D11DeviceContext ctx, ID3D11Texture2D dest,
                               uint timeoutMs = 0)
    {
        // After Ctrl+Alt+Del / lock / RDP the duplication is access-lost;
        // _dupl may be null until we successfully rebuild it.
        if (_dupl is null)
        {
            TryRecreate();
            return false;
        }

        IDXGIResource? desktopResource = null;
        try
        {
            Result r = _dupl.AcquireNextFrame(timeoutMs, out _, out desktopResource);
            if (r == Vortice.DXGI.ResultCode.WaitTimeout)
                return false;
            if (r.Failure || desktopResource is null)
            {
                // Access lost (secure desktop, mode change) — rebuild.
                Debug.WriteLine($"[DesktopDuplicator] acquire failed: {r}");
                TryRecreate();
                return false;
            }

            using var src = desktopResource.QueryInterface<ID3D11Texture2D>();
            ctx.CopyResource(dest, src);
            return true;
        }
        catch (SharpGenException ex)
        {
            Debug.WriteLine($"[DesktopDuplicator] {ex.ResultCode}: {ex.Message}");
            TryRecreate();
            return false;
        }
        finally
        {
            desktopResource?.Dispose();
            try { _dupl?.ReleaseFrame(); } catch { /* ignore */ }
        }
    }

    /// <summary>
    /// Rebuild the duplication object, throttled so a persistent failure
    /// (e.g. while on the secure desktop) doesn't spin. Recovers
    /// automatically once the desktop is accessible again.
    /// </summary>
    private void TryRecreate()
    {
        double now = _recreateClock.Elapsed.TotalMilliseconds;
        if (now - _lastRecreateMs < 500)
            return;
        _lastRecreateMs = now;
        try
        {
            _dupl?.Dispose();
            _dupl = _output1?.DuplicateOutput(_device);
        }
        catch
        {
            _dupl = null; // try again on the next throttled tick
        }
    }

    public void Dispose()
    {
        _dupl?.Dispose();
        _output1?.Dispose();
        _dupl = null;
        _output1 = null;
    }
}
