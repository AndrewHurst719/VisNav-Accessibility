using System.Windows;

namespace VisNav.App;

/// <summary>
/// A block of readable text discovered on screen, with its bounding rectangle in
/// physical screen pixels (UI Automation coordinates).
/// </summary>
public sealed class TextGroup
{
    public TextGroup(string text, Rect bounds)
    {
        Text = text;
        Bounds = bounds;
    }

    public string Text { get; }

    /// <summary>Bounding rectangle in physical screen pixels.</summary>
    public Rect Bounds { get; }
}
