using System.Diagnostics;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using VrDesktopBridge.Capture;

namespace VrDesktopBridge.Render;

public enum RenderMode
{
    /// <summary>M1: one full-window copy.</summary>
    Single,
    /// <summary>M2+: same image drawn into left and right halves (SBS).</summary>
    SideBySide,
}

/// <summary>
/// Owns the D3D11 device + a DXGI swap chain on a child HWND, captures the
/// desktop via <see cref="DesktopDuplicator"/>, and draws it with a
/// fullscreen-triangle shader. SBS mode draws the same texture into the
/// left and right halves via two viewports (no per-eye distortion in v1).
/// </summary>
public sealed class SbsRenderer : IDisposable
{
    private const string ShaderSource = @"
struct VSOut { float4 pos : SV_Position; float2 uv : TEXCOORD0; };
VSOut VS(uint id : SV_VertexID)
{
    VSOut o;
    float2 t = float2((id << 1) & 2, id & 2);
    o.uv  = t;
    o.pos = float4(t * float2(2, -2) + float2(-1, 1), 0, 1);
    return o;
}
Texture2D    tex : register(t0);
SamplerState smp : register(s0);
float4 PS(VSOut i) : SV_Target { return tex.Sample(smp, i.uv); }

// VR lens (barrel) distortion resolve pass. Pre-distorts each eye so a
// Cardboard-type lens turns it back to rectilinear.
cbuffer P : register(b0)
{
    float2 lensCenter;   // eye optical center in [0,1]
    float2 uvOff;        // x = U offset into scene tex, y = U scale (0.5)
    float  k1; float k2; // radial distortion coefficients
    float  en; float pad;// en>0.5 enables distortion
};
float4 PSD(VSOut i) : SV_Target
{
    float2 c  = i.uv - lensCenter;
    float  r2 = dot(c, c);
    float  f  = (en > 0.5) ? (1.0 + k1 * r2 + k2 * r2 * r2) : 1.0;
    float2 du = lensCenter + c * f;
    if (du.x < 0.0 || du.x > 1.0 || du.y < 0.0 || du.y > 1.0)
        return float4(0, 0, 0, 1);
    float2 su = float2(uvOff.x + du.x * uvOff.y, du.y);
    return tex.Sample(smp, su);
}

// Guide-label pass: sample the label and scale its alpha for fade-out.
cbuffer G : register(b0) { float gOpacity; float3 _gpad; };
float4 PSG(VSOut i) : SV_Target
{
    float4 c = tex.Sample(smp, i.uv);
    c.a *= saturate(gOpacity);
    return c;
}

// Calibration test pattern (no texture): grid + concentric circles +
// centre cross/dot + content border, in content-rect UV. x is corrected
// by the rect aspect so circles are round and the grid is square.
cbuffer Q : register(b0) { float pAspect; float3 _qpad; };
float4 PSP(VSOut i) : SV_Target
{
    float2 p = float2((i.uv.x - 0.5) * pAspect, i.uv.y - 0.5);
    float  r = length(p);
    float3 col = float3(0.05, 0.05, 0.07);                 // background
    // square grid every 0.1 (distance to nearest multiple of 0.1)
    float2 g = abs(frac(p / 0.1 + 0.5) - 0.5) * 0.1;
    if (min(g.x, g.y) < 0.002) col = float3(0.0, 0.32, 0.08);
    // concentric circles every r=0.15 around the lens-aligned centre
    float dc = abs(frac(r / 0.15 + 0.5) - 0.5) * 0.15;
    if (dc < 0.0025 && r > 0.05) col = float3(0.15, 0.75, 1.0);
    // full-span centre cross + dot (binocular fusion target)
    if (abs(p.x) < 0.003 || abs(p.y) < 0.003) col = float3(1.0, 1.0, 1.0);
    if (r < 0.012) col = float3(1.0, 0.25, 0.2);
    // content-rect border (reveals when a shift got clamped/eaten)
    if (i.uv.x < 0.006 || i.uv.x > 0.994 ||
        i.uv.y < 0.006 || i.uv.y > 0.994)
        col = float3(1.0, 0.55, 0.1);
    return float4(col, 1.0);
}
";

    [System.Runtime.InteropServices.StructLayout(
        System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct GuideCB
    {
        public float Opacity, P0, P1, P2; // 16 bytes
    }

    [System.Runtime.InteropServices.StructLayout(
        System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct DistortCB
    {
        public float Cx, Cy, UOff, UScale, K1, K2, En, Pad; // 32 bytes
    }

    [System.Runtime.InteropServices.StructLayout(
        System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct PatternCB
    {
        public float Aspect, P0, P1, P2; // 16 bytes
    }

    private ID3D11Device _device = null!;
    private ID3D11DeviceContext _ctx = null!;
    private IDXGISwapChain1 _swapChain = null!;
    private ID3D11RenderTargetView _rtv = null!;
    private ID3D11VertexShader _vs = null!;
    private ID3D11PixelShader _ps = null!;
    private ID3D11PixelShader _psDistort = null!;
    private ID3D11SamplerState _sampler = null!;

    // Offscreen target: the full SBS (desktop + cursor) is rendered here
    // first, then the distortion pass samples it into the back buffer.
    private ID3D11Texture2D? _sceneTex;
    private ID3D11RenderTargetView? _sceneRtv;
    private ID3D11ShaderResourceView? _sceneSrv;
    private ID3D11Buffer _cb = null!;

    public bool DistortionEnabled { get; set; } = true;
    public float K1 { get; set; } = 0.22f;
    public float K2 { get; set; } = 0.10f;

    /// <summary>
    /// Horizontal IPD shift as a fraction of one eye's half-width
    /// (-0.15..+0.15). Positive shifts both eyes' content (and the
    /// distortion lens-centre) TOWARD the centre seam — narrowing the
    /// inter-image distance so it matches the user's IPD / lens spacing.
    /// 0 = current behaviour (geometric half-centres).
    /// </summary>
    public float IpdShift { get; set; } = 0f;

    /// <summary>
    /// Common view offset applied to BOTH eyes, as a fraction of one
    /// eye's half-width (+ = right). Compensates the phone sitting
    /// left/right of centre in the goggle tray (asymmetric eyes).
    /// </summary>
    public float OffsetX { get; set; } = 0f;

    /// <summary>
    /// Common vertical view offset for BOTH eyes, fraction of the
    /// screen height (+ = down). Compensates the phone sitting
    /// high/low relative to the lens centres.
    /// </summary>
    public float OffsetY { get; set; } = 0f;

    /// <summary>
    /// Vertical difference BETWEEN the eyes, fraction of the screen
    /// height (+ = right-eye image lower / left-eye higher, split
    /// half-and-half). Vertical disparity is the classic "each eye is
    /// fine alone, together they never lock" fusion killer.
    /// </summary>
    public float EyeYDiff { get; set; } = 0f;

    /// <summary>Show the calibration test pattern instead of the desktop.</summary>
    public bool TestPattern { get; set; }

    private DesktopDuplicator _dupl = null!;
    private ID3D11Texture2D? _frameTex;
    private ID3D11ShaderResourceView? _frameSrv;

    private readonly CursorOverlay _cursor = new();
    private ID3D11Texture2D? _cursorTex;
    private ID3D11ShaderResourceView? _cursorSrv;
    private ID3D11BlendState _alphaBlend = null!;
    private int _cursorVersion = -1;

    // First-run guide labels (built once on first shown frame).
    private ID3D11Texture2D? _guideLTex, _guideRTex, _guideCTex;
    private ID3D11ShaderResourceView? _guideLSrv, _guideRSrv, _guideCSrv;
    private int _guideLW, _guideLH, _guideRW, _guideRH, _guideCW, _guideCH;
    private bool _guideBuilt;

    private ID3D11PixelShader _psGuide = null!;
    private ID3D11Buffer _guideCb = null!;

    private ID3D11PixelShader _psPattern = null!;
    private ID3D11Buffer _patternCb = null!;

    /// <summary>Show the first-run operation guide (host controls timeout).</summary>
    public bool ShowGuide { get; set; } = true;

    /// <summary>Guide fade alpha 0..1 (host animates this for fade-out).</summary>
    public float GuideOpacity { get; set; } = 1f;

    private int _width;
    private int _height;
    private bool _haveFrame;

    public RenderMode Mode { get; set; } = RenderMode.Single;

    /// <summary>
    /// Scale of the mirrored image within each eye (1.0 = current/100%).
    /// Lower values shrink the image (more black border) — lets the user
    /// step the apparent screen size in VR. Cursor scales with it.
    /// </summary>
    public float ContentScale { get; set; } = 1f;

    /// <summary>
    /// Duplicate-mode: the real OS cursor is hidden, so draw a fixed
    /// standard arrow at the pointer position instead of snapshotting
    /// the (now invisible) live cursor.
    /// </summary>
    public bool UseStandardArrow
    {
        get => _cursor.ForceStandardArrow;
        set => _cursor.ForceStandardArrow = value;
    }

    public void Initialize(IntPtr hwnd, int width, int height)
    {
        _width = Math.Max(1, width);
        _height = Math.Max(1, height);

        D3D11.D3D11CreateDevice(
            null!, DriverType.Hardware,
            DeviceCreationFlags.BgraSupport,
            new[] { FeatureLevel.Level_11_1, FeatureLevel.Level_11_0 },
            out ID3D11Device device,
            out ID3D11DeviceContext ctx).CheckError();
        _device = device;
        _ctx = ctx;

        using var dxgiFactory = DXGI.CreateDXGIFactory1<IDXGIFactory2>();
        var scDesc = new SwapChainDescription1
        {
            Width = (uint)_width,
            Height = (uint)_height,
            Format = Format.B8G8R8A8_UNorm,
            Stereo = false,
            SampleDescription = new SampleDescription(1, 0),
            BufferUsage = Usage.RenderTargetOutput,
            BufferCount = 2,
            Scaling = Scaling.Stretch,
            SwapEffect = SwapEffect.FlipDiscard,
            AlphaMode = Vortice.DXGI.AlphaMode.Ignore,
            Flags = SwapChainFlags.None,
        };
        _swapChain = dxgiFactory.CreateSwapChainForHwnd(_device, hwnd, scDesc);

        CreateRenderTarget();
        CompileShaders();

        _sampler = _device.CreateSamplerState(new SamplerDescription
        {
            Filter = Filter.MinMagMipLinear,
            AddressU = TextureAddressMode.Clamp,
            AddressV = TextureAddressMode.Clamp,
            AddressW = TextureAddressMode.Clamp,
            ComparisonFunc = ComparisonFunction.Never,
            MinLOD = 0,
            MaxLOD = float.MaxValue,
        });

        // Straight-alpha blend for the cursor overlay pass.
        _alphaBlend = _device.CreateBlendState(
            new BlendDescription(Blend.SourceAlpha, Blend.InverseSourceAlpha));

        var cbDesc = new BufferDescription(
            (uint)System.Runtime.InteropServices.Marshal.SizeOf<DistortCB>(),
            BindFlags.ConstantBuffer, ResourceUsage.Default,
            CpuAccessFlags.None, ResourceOptionFlags.None, 0);
        _cb = _device.CreateBuffer(ref cbDesc, IntPtr.Zero);

        var gcbDesc = new BufferDescription(
            (uint)System.Runtime.InteropServices.Marshal.SizeOf<GuideCB>(),
            BindFlags.ConstantBuffer, ResourceUsage.Default,
            CpuAccessFlags.None, ResourceOptionFlags.None, 0);
        _guideCb = _device.CreateBuffer(ref gcbDesc, IntPtr.Zero);

        var pcbDesc = new BufferDescription(
            (uint)System.Runtime.InteropServices.Marshal.SizeOf<PatternCB>(),
            BindFlags.ConstantBuffer, ResourceUsage.Default,
            CpuAccessFlags.None, ResourceOptionFlags.None, 0);
        _patternCb = _device.CreateBuffer(ref pcbDesc, IntPtr.Zero);

        _dupl = new DesktopDuplicator(_device, outputIndex: 0);
        CreateFrameTexture();
        CreateSceneTarget();
    }

    private void CreateSceneTarget()
    {
        _sceneSrv?.Dispose();
        _sceneRtv?.Dispose();
        _sceneTex?.Dispose();

        var desc = new Texture2DDescription
        {
            Width = (uint)_width,
            Height = (uint)_height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Default,
            BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
            CPUAccessFlags = CpuAccessFlags.None,
            MiscFlags = ResourceOptionFlags.None,
        };
        _sceneTex = _device.CreateTexture2D(desc);
        _sceneRtv = _device.CreateRenderTargetView(_sceneTex);
        _sceneSrv = _device.CreateShaderResourceView(_sceneTex);
    }

    private void CreateRenderTarget()
    {
        using var backBuffer = _swapChain.GetBuffer<ID3D11Texture2D>(0);
        _rtv = _device.CreateRenderTargetView(backBuffer);
    }

    private void CompileShaders()
    {
        ReadOnlyMemory<byte> vsCode =
            Compiler.Compile(ShaderSource, "VS", "sbs.hlsl", "vs_5_0");
        ReadOnlyMemory<byte> psCode =
            Compiler.Compile(ShaderSource, "PS", "sbs.hlsl", "ps_5_0");
        ReadOnlyMemory<byte> psdCode =
            Compiler.Compile(ShaderSource, "PSD", "sbs.hlsl", "ps_5_0");
        ReadOnlyMemory<byte> psgCode =
            Compiler.Compile(ShaderSource, "PSG", "sbs.hlsl", "ps_5_0");
        ReadOnlyMemory<byte> pspCode =
            Compiler.Compile(ShaderSource, "PSP", "sbs.hlsl", "ps_5_0");
        _vs = _device.CreateVertexShader(vsCode.Span);
        _ps = _device.CreatePixelShader(psCode.Span);
        _psDistort = _device.CreatePixelShader(psdCode.Span);
        _psGuide = _device.CreatePixelShader(psgCode.Span);
        _psPattern = _device.CreatePixelShader(pspCode.Span);
    }

    private void CreateFrameTexture()
    {
        _frameSrv?.Dispose();
        _frameTex?.Dispose();

        var desc = new Texture2DDescription
        {
            Width = (uint)Math.Max(1, _dupl.OutputWidth),
            Height = (uint)Math.Max(1, _dupl.OutputHeight),
            MipLevels = 1,
            ArraySize = 1,
            Format = _dupl.OutputFormat,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Default,
            BindFlags = BindFlags.ShaderResource,
            CPUAccessFlags = CpuAccessFlags.None,
            MiscFlags = ResourceOptionFlags.None,
        };
        _frameTex = _device.CreateTexture2D(desc);
        _frameSrv = _device.CreateShaderResourceView(_frameTex);
    }

    public void Resize(int width, int height)
    {
        if (_swapChain is null) return;
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        if (width == _width && height == _height) return;

        _width = width;
        _height = height;
        _rtv?.Dispose();
        _swapChain.ResizeBuffers(2, (uint)_width, (uint)_height,
            Format.B8G8R8A8_UNorm, SwapChainFlags.None);
        CreateRenderTarget();
        CreateSceneTarget();
    }

    /// <summary>
    /// "Contain" fit: largest rect with the source aspect ratio that fits
    /// inside the destination, centered. Remaining area stays the cleared
    /// black background (letterbox / pillarbox bars).
    /// </summary>
    private static Viewport Fit(int srcW, int srcH,
                                float dstX, float dstY, float dstW, float dstH)
    {
        if (srcW <= 0 || srcH <= 0 || dstW <= 0 || dstH <= 0)
            return new Viewport(dstX, dstY, Math.Max(1f, dstW),
                                Math.Max(1f, dstH), 0f, 1f);

        float scale = Math.Min(dstW / srcW, dstH / srcH);
        float w = srcW * scale;
        float h = srcH * scale;
        float x = dstX + (dstW - w) * 0.5f;
        float y = dstY + (dstH - h) * 0.5f;
        return new Viewport(x, y, w, h, 0f, 1f);
    }

    /// <summary>(Re)upload the cursor bitmap if its shape changed.</summary>
    private bool EnsureCursorTexture()
    {
        _cursor.Update();
        if (!_cursor.Visible || _cursor.Pixels.Length == 0)
            return false;

        if (_cursorTex is not null && _cursor.ShapeVersion == _cursorVersion)
            return true;

        _cursorSrv?.Dispose();
        _cursorTex?.Dispose();
        _cursorSrv = null;
        _cursorTex = null;

        int w = _cursor.Width, h = _cursor.Height;
        var desc = new Texture2DDescription
        {
            Width = (uint)w,
            Height = (uint)h,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Immutable,
            BindFlags = BindFlags.ShaderResource,
            CPUAccessFlags = CpuAccessFlags.None,
            MiscFlags = ResourceOptionFlags.None,
        };
        var gch = System.Runtime.InteropServices.GCHandle.Alloc(
            _cursor.Pixels, System.Runtime.InteropServices.GCHandleType.Pinned);
        try
        {
            var sub = new SubresourceData(gch.AddrOfPinnedObject(), (uint)(w * 4), 0u);
            _cursorTex = _device.CreateTexture2D(desc, sub);
            _cursorSrv = _device.CreateShaderResourceView(_cursorTex);
        }
        finally
        {
            gch.Free();
        }
        _cursorVersion = _cursor.ShapeVersion;
        return true;
    }

    /// <summary>Draw the cursor into one eye's fitted image rect.</summary>
    private void DrawCursorInEye(Viewport eye, int srcW, int srcH)
    {
        float scale = eye.Width / srcW;            // uniform (Fit keeps ratio)
        int lx = _cursor.ScreenX - _dupl.OutputX;  // cursor pos on captured mon
        int ly = _cursor.ScreenY - _dupl.OutputY;
        if (lx < 0 || ly < 0 || lx >= srcW || ly >= srcH)
            return; // cursor is not on the mirrored monitor

        float dx = eye.X + (lx - _cursor.HotspotX) * scale;
        float dy = eye.Y + (ly - _cursor.HotspotY) * scale;
        float dw = Math.Max(1f, _cursor.Width * scale);
        float dh = Math.Max(1f, _cursor.Height * scale);

        _ctx.PSSetShaderResource(0, _cursorSrv!);
        _ctx.RSSetViewport(new Viewport(dx, dy, dw, dh, 0f, 1f));
        _ctx.Draw(3, 0);
    }

    private (ID3D11Texture2D tex, ID3D11ShaderResourceView srv)
        CreateBgraTexture(byte[] px, int w, int h)
    {
        var desc = new Texture2DDescription
        {
            Width = (uint)w, Height = (uint)h, MipLevels = 1, ArraySize = 1,
            Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Immutable,
            BindFlags = BindFlags.ShaderResource,
            CPUAccessFlags = CpuAccessFlags.None,
            MiscFlags = ResourceOptionFlags.None,
        };
        var gch = System.Runtime.InteropServices.GCHandle.Alloc(
            px, System.Runtime.InteropServices.GCHandleType.Pinned);
        try
        {
            var sub = new SubresourceData(gch.AddrOfPinnedObject(), (uint)(w * 4), 0u);
            var t = _device.CreateTexture2D(desc, sub);
            return (t, _device.CreateShaderResourceView(t));
        }
        finally { gch.Free(); }
    }

    private void EnsureGuideTextures()
    {
        if (_guideBuilt) return;
        _guideBuilt = true; // build once; never retry on failure
        try
        {
            var l = GuideLabel.Build("END", "[Esc → Esc]");
            var c = GuideLabel.Build("CENTER", "[Ctrl+Alt+Shift+←/→]");
            var r = GuideLabel.Build("ZOOM", "[Ctrl+Alt+Shift+↑/↓]");
            (_guideLTex, _guideLSrv) = CreateBgraTexture(l.Pixels, l.Width, l.Height);
            _guideLW = l.Width; _guideLH = l.Height;
            (_guideCTex, _guideCSrv) = CreateBgraTexture(c.Pixels, c.Width, c.Height);
            _guideCW = c.Width; _guideCH = c.Height;
            (_guideRTex, _guideRSrv) = CreateBgraTexture(r.Pixels, r.Width, r.Height);
            _guideRW = r.Width; _guideRH = r.Height;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GuideOverlay] build failed: {ex.Message}");
        }
    }

    /// <summary>Draw the top-left/top-right guide labels inside one eye.</summary>
    private void DrawGuideInEye(Viewport e)
    {
        if (_guideLSrv is null || _guideRSrv is null) return;

        // ~3x larger than before; placed near the TOP, horizontally
        // centred at 1/4, 1/2 and 3/4 of the eye (END / CENTER / ZOOM).
        float top = e.Y + e.Height * 0.04f;
        float gh = Math.Clamp(e.Height * 0.15f, 42f, 220f);

        float lw = _guideLW * (gh / _guideLH);
        float lx = e.X + e.Width * 0.25f - lw * 0.5f;
        lx = Math.Clamp(lx, e.X, e.X + e.Width - lw);
        _ctx.PSSetShaderResource(0, _guideLSrv);
        _ctx.RSSetViewport(new Viewport(lx, top, lw, gh, 0f, 1f));
        _ctx.Draw(3, 0);

        if (_guideCSrv is not null)
        {
            float cw = _guideCW * (gh / _guideCH);
            float cx = e.X + e.Width * 0.5f - cw * 0.5f;
            cx = Math.Clamp(cx, e.X, e.X + e.Width - cw);
            _ctx.PSSetShaderResource(0, _guideCSrv);
            _ctx.RSSetViewport(new Viewport(cx, top, cw, gh, 0f, 1f));
            _ctx.Draw(3, 0);
        }

        float rw = _guideRW * (gh / _guideRH);
        float rx = e.X + e.Width * 0.75f - rw * 0.5f;
        rx = Math.Clamp(rx, e.X, e.X + e.Width - rw);
        _ctx.PSSetShaderResource(0, _guideRSrv);
        _ctx.RSSetViewport(new Viewport(rx, top, rw, gh, 0f, 1f));
        _ctx.Draw(3, 0);
    }

    /// <summary>Capture + draw + present one frame.</summary>
    public void RenderFrame()
    {
        if (_swapChain is null || _frameTex is null) return;

        if (_dupl.TryCopyFrameTo(_ctx, _frameTex))
            _haveFrame = true;

        if (!_haveFrame) return; // nothing captured yet — keep window blank

        bool haveCursor = EnsureCursorTexture();

        int srcW = _dupl.OutputWidth;
        int srcH = _dupl.OutputHeight;
        int half = _width / 2;
        bool sbs = Mode == RenderMode.SideBySide;
        int eyeCount = sbs ? 2 : 1;

        // ---- Pass 1: render the (rectilinear) SBS into the offscreen ----
        var sceneRt = _sceneRtv;
        _ctx.OMSetRenderTargets(sceneRt);
        _ctx.ClearRenderTargetView(sceneRt, new Color4(0f, 0f, 0f, 1f));
        _ctx.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        _ctx.VSSetShader(_vs);
        _ctx.PSSetShader(_ps);
        _ctx.PSSetSampler(0, _sampler);

        Span<Viewport> eyes = stackalloc Viewport[2];
        if (sbs)
        {
            eyes[0] = Fit(srcW, srcH, 0, 0, half, _height);
            eyes[1] = Fit(srcW, srcH, half, 0, _width - half, _height);
        }
        else
        {
            eyes[0] = Fit(srcW, srcH, 0, 0, _width, _height);
        }

        // Apply the stepped content scale (shrink about each eye's centre;
        // both desktop and cursor use eyes[] so they stay aligned).
        float s = ContentScale <= 0f ? 1f : ContentScale;
        if (s != 1f)
            for (int i = 0; i < eyeCount; i++)
            {
                var v = eyes[i];
                float w = v.Width * s, h = v.Height * s;
                eyes[i] = new Viewport(
                    v.X + (v.Width - w) * 0.5f,
                    v.Y + (v.Height - h) * 0.5f,
                    w, h, 0f, 1f);
            }

        // View shifts: IPD convergence (symmetric, toward the seam) +
        // common X/Y offset (phone placement in the goggles) + per-eye
        // vertical difference (vertical fusion). Clamp inside each half
        // so we never bleed across the seam, and record the ACTUAL
        // applied shift so the Pass 2 lens centre follows the content
        // centre even when a shift gets clamped (M6 invariant).
        Span<float> dxA = stackalloc float[2];
        Span<float> dyA = stackalloc float[2];
        dxA[0] = dxA[1] = dyA[0] = dyA[1] = 0f;
        if (sbs)
        {
            for (int i = 0; i < 2; i++)
            {
                var v = eyes[i];
                float dx = ((i == 0 ? +IpdShift : -IpdShift) + OffsetX) * half;
                float dy = (OffsetY + (i == 0 ? -0.5f : +0.5f) * EyeYDiff)
                           * _height;
                float minX = i == 0 ? 0f : half;
                float maxX = (i == 0 ? half : _width) - v.Width;
                float nx = Math.Clamp(v.X + dx, minX, Math.Max(minX, maxX));
                float ny = Math.Clamp(v.Y + dy, 0f,
                    Math.Max(0f, _height - v.Height));
                dxA[i] = nx - v.X;
                dyA[i] = ny - v.Y;
                eyes[i] = new Viewport(nx, ny, v.Width, v.Height, 0f, 1f);
            }
        }

        _ctx.OMSetBlendState(null);
        if (TestPattern)
        {
            // Calibration pattern replaces the desktop (cursor skipped);
            // it goes through the same viewports + distortion pass, so
            // what you align is exactly what the desktop will get.
            var pcb = new PatternCB
            {
                Aspect = eyes[0].Height > 0f
                    ? eyes[0].Width / eyes[0].Height : 1f,
            };
            _ctx.UpdateSubresource(ref pcb, _patternCb, 0u, 0u, 0u, null);
            _ctx.PSSetShader(_psPattern);
            _ctx.PSSetConstantBuffer(0, _patternCb);
            for (int i = 0; i < eyeCount; i++)
            {
                _ctx.RSSetViewport(eyes[i]);
                _ctx.Draw(3, 0);
            }
            _ctx.PSSetShader(_ps); // restore for guide/cursor passes
        }
        else
        {
            _ctx.PSSetShaderResource(0, _frameSrv!);
            for (int i = 0; i < eyeCount; i++)
            {
                _ctx.RSSetViewport(eyes[i]);
                _ctx.Draw(3, 0);
            }
            if (haveCursor && _cursorSrv is not null)
            {
                _ctx.OMSetBlendState(_alphaBlend);
                for (int i = 0; i < eyeCount; i++)
                    DrawCursorInEye(eyes[i], srcW, srcH);
                _ctx.OMSetBlendState(null);
            }
        }

        // First-run operation guide (top-left End / top-right Zoom),
        // alpha-scaled by GuideOpacity for the fade-out.
        if (ShowGuide && GuideOpacity > 0.001f)
        {
            EnsureGuideTextures();
            var gcb = new GuideCB { Opacity = GuideOpacity };
            _ctx.UpdateSubresource(ref gcb, _guideCb, 0u, 0u, 0u, null);
            _ctx.PSSetShader(_psGuide);
            _ctx.PSSetConstantBuffer(0, _guideCb);
            _ctx.OMSetBlendState(_alphaBlend);
            for (int i = 0; i < eyeCount; i++)
                DrawGuideInEye(eyes[i]);
            _ctx.OMSetBlendState(null);
        }

        // ---- Pass 2: barrel-distortion resolve into the back buffer ----
        _ctx.OMSetRenderTargets(_rtv);
        _ctx.ClearRenderTargetView(_rtv, new Color4(0f, 0f, 0f, 1f));
        _ctx.VSSetShader(_vs);
        _ctx.PSSetShader(_psDistort);
        _ctx.PSSetSampler(0, _sampler);
        _ctx.PSSetShaderResource(0, _sceneSrv!);
        _ctx.PSSetConstantBuffer(0, _cb);

        float en = DistortionEnabled ? 1f : 0f;
        for (int i = 0; i < eyeCount; i++)
        {
            float uOff, uScale;
            Viewport vp;
            if (!sbs)
            {
                uOff = 0f; uScale = 1f;
                vp = new Viewport(0, 0, _width, _height, 0f, 1f);
            }
            else if (i == 0)
            {
                uOff = 0f; uScale = (float)half / _width;
                vp = new Viewport(0, 0, half, _height, 0f, 1f);
            }
            else
            {
                uOff = (float)half / _width;
                uScale = (float)(_width - half) / _width;
                vp = new Viewport(half, 0, _width - half, _height, 0f, 1f);
            }

            // Lens centre matches the ACTUAL (post-clamp) content shift
            // from Pass 1, in this eye's viewport UV units.
            float eyeW = !sbs ? _width : (i == 0 ? half : _width - half);
            float cx = 0.5f + (sbs ? dxA[i] / eyeW : 0f);
            float cy = 0.5f + (sbs ? dyA[i] / _height : 0f);
            var cb = new DistortCB
            {
                Cx = cx, Cy = cy,
                UOff = uOff, UScale = uScale,
                K1 = K1, K2 = K2, En = en, Pad = 0f,
            };
            _ctx.UpdateSubresource(ref cb, _cb, 0u, 0u, 0u, null);
            _ctx.RSSetViewport(vp);
            _ctx.Draw(3, 0);
        }

        _swapChain.Present(1, PresentFlags.None);
    }

    public void Dispose()
    {
        try
        {
            _dupl?.Dispose();
            _cursorSrv?.Dispose();
            _cursorTex?.Dispose();
            _guideLSrv?.Dispose();
            _guideCSrv?.Dispose();
            _guideCTex?.Dispose();
            _guideLTex?.Dispose();
            _guideRSrv?.Dispose();
            _guideRTex?.Dispose();
            _alphaBlend?.Dispose();
            _frameSrv?.Dispose();
            _frameTex?.Dispose();
            _sceneSrv?.Dispose();
            _sceneRtv?.Dispose();
            _sceneTex?.Dispose();
            _cb?.Dispose();
            _guideCb?.Dispose();
            _patternCb?.Dispose();
            _sampler?.Dispose();
            _psPattern?.Dispose();
            _psGuide?.Dispose();
            _psDistort?.Dispose();
            _ps?.Dispose();
            _vs?.Dispose();
            _rtv?.Dispose();
            _swapChain?.Dispose();
            _ctx?.Dispose();
            _device?.Dispose();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SbsRenderer] dispose: {ex.Message}");
        }
    }
}
