using System.Windows.Media;
using VisNav.Core.Settings;

namespace VisNav.App;

/// <summary>
/// Single source of truth for the crosshair color presets, shared by the overlay
/// renderer and the settings swatches so they never drift apart.
/// </summary>
public static class CrosshairPalette
{
    /// <summary>Presets in the order they appear in the settings UI.</summary>
    public static readonly IReadOnlyList<CrosshairColor> Order = new[]
    {
        CrosshairColor.Green,
        CrosshairColor.Red,
        CrosshairColor.WhiteOnBlack,
        CrosshairColor.Cyan,
        CrosshairColor.NeonPink,
    };

    public static string DisplayName(CrosshairColor color) => color switch
    {
        CrosshairColor.Green => "Green",
        CrosshairColor.Red => "Red",
        CrosshairColor.WhiteOnBlack => "White",
        CrosshairColor.Cyan => "Cyan",
        CrosshairColor.NeonPink => "Pink",
        _ => color.ToString(),
    };

    /// <summary>Primary ring color.</summary>
    public static Color Stroke(CrosshairColor color) => color switch
    {
        CrosshairColor.Green => Color.FromRgb(0x00, 0xE0, 0x00),
        CrosshairColor.Red => Color.FromRgb(0xFF, 0x2A, 0x2A),
        CrosshairColor.WhiteOnBlack => Colors.White,
        CrosshairColor.Cyan => Color.FromRgb(0x00, 0xE5, 0xFF),
        CrosshairColor.NeonPink => Color.FromRgb(0xFF, 0x3F, 0xBF),
        _ => Colors.White,
    };

    /// <summary>Whether this preset draws a contrasting outline behind the ring.</summary>
    public static bool HasOutline(CrosshairColor color) => color == CrosshairColor.WhiteOnBlack;

    /// <summary>Outline color used when <see cref="HasOutline"/> is true.</summary>
    public static Color OutlineColor => Colors.Black;
}
