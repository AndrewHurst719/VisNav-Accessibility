namespace VisNav.Core.Settings;

/// <summary>
/// Root settings object for the app. Each feature has its own sub-section so the
/// three overlays can be configured and toggled independently. Serialized to
/// <c>%AppData%\VisNav\settings.json</c> by <see cref="SettingsService"/>.
/// </summary>
public sealed class VisNavSettings
{
    public CrosshairSettings Crosshair { get; set; } = new();
    public MagnifierSettings Magnifier { get; set; } = new();
    public NarratorSettings Narrator { get; set; } = new();
}

/// <summary>
/// Preset crosshair colors. <see cref="WhiteOnBlack"/> renders a white ring with a
/// black outline so it stays visible on any background.
/// </summary>
public enum CrosshairColor
{
    Green,
    Red,
    WhiteOnBlack,
    Cyan,
    NeonPink,
}

/// <summary>
/// A thin circle that follows the cursor, centered on the pointer. Subtle by design —
/// easy to locate without obscuring content.
/// </summary>
public sealed class CrosshairSettings
{
    public bool Enabled { get; set; }

    /// <summary>Ring color preset.</summary>
    public CrosshairColor Color { get; set; } = CrosshairColor.Green;

    /// <summary>Outer circle radius in device-independent pixels.</summary>
    public int OuterRadius { get; set; } = 40;

    /// <summary>Outer circle line thickness in device-independent pixels.</summary>
    public int OuterThickness { get; set; } = 2;

    /// <summary>Ring opacity as a percentage, 10–100 (100 = fully opaque).</summary>
    public int OpacityPercent { get; set; } = 100;
}

/// <summary>How the activation hotkey behaves.</summary>
public enum MagnifierActivation
{
    /// <summary>Press the chord to turn on; press again to turn off.</summary>
    Toggle,

    /// <summary>Active only while the chord is held down.</summary>
    Hold,
}

/// <summary>
/// Magnification-API lens that follows the cursor, shown/hidden by a configurable
/// global hotkey. Lens dimensions are in physical pixels.
/// </summary>
public sealed class MagnifierSettings
{
    /// <summary>Whether the magnifier feature is armed (its hotkey responds).</summary>
    public bool Enabled { get; set; }

    /// <summary>Zoom multiplier (1.0 = no zoom).</summary>
    public double ZoomFactor { get; set; } = 2.0;

    /// <summary>Lens width in physical pixels.</summary>
    public int LensWidth { get; set; } = 480;

    /// <summary>Lens height in physical pixels.</summary>
    public int LensHeight { get; set; } = 320;

    /// <summary>Toggle vs hold-to-magnify.</summary>
    public MagnifierActivation Activation { get; set; } = MagnifierActivation.Toggle;

    /// <summary>
    /// Activation hotkey as up to 3 Windows virtual-key codes held together. Mouse
    /// buttons are VK codes too (VK_RBUTTON = 2, VK_MBUTTON = 4, …). Default is
    /// Ctrl (0x11) + Shift (0x10) + F (0x46) — keyboard-only so it never disturbs
    /// right-click context menus.
    /// </summary>
    public List<int> ActivationChord { get; set; } = new() { 0x11, 0x10, 0x46 };
}

/// <summary>
/// UI Automation + text-to-speech narrator. A global hotkey scans the focused window for
/// text blocks and shows a "▶ Read" button on each; clicking one reads it aloud.
/// </summary>
public sealed class NarratorSettings
{
    public bool Enabled { get; set; }

    /// <summary>Preferred SAPI voice name; <c>null</c> uses the system default.</summary>
    public string? VoiceName { get; set; }

    /// <summary>Speech rate in the SAPI range -10 (slowest) to 10 (fastest).</summary>
    public int SpeechRate { get; set; }

    /// <summary>
    /// Hotkey (up to 3 VK codes) that scans the focused window for readable text.
    /// Default Ctrl (0x11) + Shift (0x10) + R (0x52).
    /// </summary>
    public List<int> ScanChord { get; set; } = new() { 0x11, 0x10, 0x52 };

    /// <summary>
    /// How aggressively detected text is grouped, 1–10. Higher = smaller, tighter groups
    /// (sentences/paragraphs); lower = larger groups merging across bigger gaps (multiple
    /// paragraphs). 5 is the balanced default.
    /// </summary>
    public int GroupingIntensity { get; set; } = 5;

    /// <summary>
    /// Hotkey (up to 3 VK codes) for the "read a region" tool: drag a box and the text inside
    /// is OCR'd and read aloud. Default Ctrl (0x11) + Shift (0x10) + Q (0x51).
    /// </summary>
    public List<int> ReadRegionChord { get; set; } = new() { 0x11, 0x10, 0x51 };

    /// <summary>Global pause/resume toggle for speech. Default Ctrl + Shift + P (0x50).</summary>
    public List<int> PauseChord { get; set; } = new() { 0x11, 0x10, 0x50 };

    /// <summary>Global stop hotkey for speech. Default Ctrl + Shift + X (0x58).</summary>
    public List<int> StopChord { get; set; } = new() { 0x11, 0x10, 0x58 };
}
