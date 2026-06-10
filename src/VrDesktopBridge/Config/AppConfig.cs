using System.IO;
using System.Text.Json;

namespace VrDesktopBridge.Config;

/// <summary>
/// App configuration. Loaded from %LOCALAPPDATA%\VrDesktopBridge\config.json
/// if present; otherwise defaults are used and the file is written so the
/// user can edit it. Missing fields self-heal to defaults.
///
/// M1 only consumes <see cref="Fps"/>. Monitor-selection / recursion fields
/// are defined now and wired in M2/M3.
/// </summary>
public sealed class AppConfig
{
    /// <summary>Substring to match the CAPTURE monitor, or "primary".</summary>
    public string CaptureMonitorMatch { get; set; } = "primary";

    /// <summary>Substring to match the DISPLAY/target monitor, or "spacedesk".</summary>
    public string TargetMonitorMatch { get; set; } = "spacedesk";

    /// <summary>Target render rate. Clamped to 15..120.</summary>
    public int Fps { get; set; } = 60;

    /// <summary>Apply WDA_EXCLUDEFROMCAPTURE to the window (recursion guard).</summary>
    public bool ExcludeFromCapture { get; set; } = false;

    /// <summary>"duplicate" (same image both eyes). "stereo" reserved.</summary>
    public string SplitMode { get; set; } = "duplicate";

    /// <summary>
    /// Confine the mouse to the CAPTURED monitor so clicks (guided by the
    /// cursor visible in the headset) always land on the real mirrored
    /// windows instead of straying onto the empty spacedesk display.
    /// Toggle at runtime with Ctrl+Alt+Shift+C.
    /// </summary>
    public bool ConfineCursor { get; set; } = true;

    /// <summary>VR lens (barrel) distortion correction for Cardboard.</summary>
    public bool LensDistortion { get; set; } = true;

    /// <summary>Radial distortion coefficients (tune per viewer).</summary>
    public float DistortK1 { get; set; } = 0.22f;
    public float DistortK2 { get; set; } = 0.10f;

    /// <summary>
    /// Horizontal IPD shift, in percent of one eye's half-width (-15..+15).
    /// Positive = shift each eye's content TOWARD the centre seam (narrower
    /// inter-image distance, for users whose IPD/lens spacing is narrower
    /// than the phone's half-width — fixes "one eye out of focus" symptoms).
    /// Tune at runtime with Ctrl+Alt+Shift+→ (narrower) / ← (wider);
    /// changes are saved back here automatically (0.5%/press).
    /// </summary>
    public float IpdShiftPercent { get; set; } = 0;

    public float IpdShift => Math.Clamp(IpdShiftPercent, -15f, 15f) / 100f;

    /// <summary>
    /// Common view offset for BOTH eyes: X in percent of one eye's
    /// half-width (+ = right), Y in percent of the screen height
    /// (+ = down). Compensates the phone sitting off-centre in the
    /// goggle tray. Ctrl+Alt+Shift+J/L (X) and I/K (Y); auto-saved.
    /// </summary>
    public float OffsetXPercent { get; set; } = 0;
    public float OffsetYPercent { get; set; } = 0;

    /// <summary>
    /// Vertical difference between the eyes, in percent of the screen
    /// height (+ = right-eye image lower). Even ~0.3% breaks binocular
    /// fusion ("each eye fine alone, together never locks").
    /// Ctrl+Alt+Shift+U/O (0.25%/press); auto-saved.
    /// </summary>
    public float EyeYDiffPercent { get; set; } = 0;

    public float OffsetX => Math.Clamp(OffsetXPercent, -10f, 10f) / 100f;
    public float OffsetY => Math.Clamp(OffsetYPercent, -10f, 10f) / 100f;
    public float EyeYDiff => Math.Clamp(EyeYDiffPercent, -5f, 5f) / 100f;

    /// <summary>
    /// SBS image size at startup, in percent (50–150). Default 80%.
    /// Ctrl+Alt+Shift+Up/Down then steps from here.
    /// </summary>
    /// <summary>
    /// EXTEND mode: pull foreground windows that open on the spacedesk
    /// monitor back onto the captured (primary) monitor so they stay
    /// visible & usable in VR. Toggle with Ctrl+Alt+Shift+W.
    /// </summary>
    public bool KeepWindowsOnCapture { get; set; } = true;

    public int StartupScalePercent { get; set; } = 80;

    public float StartupScale =>
        (StartupScalePercent < 50 ? 50
            : StartupScalePercent > 150 ? 150
            : StartupScalePercent) / 100f;

    public int ClampedFps => Fps < 15 ? 15 : (Fps > 120 ? 120 : Fps);

    private static string ConfigPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VrDesktopBridge", "config.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
    };

    /// <summary>
    /// Load config, falling back to defaults. Never throws: on any error
    /// it returns defaults so the app still runs.
    /// </summary>
    public static AppConfig Load()
    {
        try
        {
            var path = ConfigPath;
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json, JsonOpts);
                if (cfg is not null)
                    return cfg;
            }
            // Write a default file the user can tweak.
            var def = new AppConfig();
            def.Save();
            return def;
        }
        catch
        {
            return new AppConfig();
        }
    }

    public void Save()
    {
        try
        {
            var path = ConfigPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOpts));
        }
        catch
        {
            // Non-fatal: config persistence is best-effort.
        }
    }
}
