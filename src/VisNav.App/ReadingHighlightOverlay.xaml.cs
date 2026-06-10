using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using VisNav.App.Interop;

namespace VisNav.App;

/// <summary>
/// A click-through, capture-excluded overlay that keeps the light-blue + yellow frame on a
/// cropped region while its text is being read aloud. Reused across reads (repositioned).
/// </summary>
public partial class ReadingHighlightOverlay : Window
{
    private double _dipScaleX = 1.0;
    private double _dipScaleY = 1.0;

    public ReadingHighlightOverlay()
    {
        InitializeComponent();
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var source = (HwndSource)PresentationSource.FromVisual(this)!;
        NativeMethods.MakeClickThrough(source.Handle);
        NativeMethods.ExcludeFromCapture(source.Handle); // don't let it taint a later OCR
        var m = source.CompositionTarget.TransformFromDevice;
        _dipScaleX = m.M11;
        _dipScaleY = m.M22;
    }

    /// <summary>Shows the highlight over a physical-pixel screen rectangle.</summary>
    public void ShowAt(int physX, int physY, int physW, int physH)
    {
        if (!IsVisible)
            Show();

        Canvas.SetLeft(Highlight, physX * _dipScaleX - Left);
        Canvas.SetTop(Highlight, physY * _dipScaleY - Top);
        Highlight.Width = physW * _dipScaleX;
        Highlight.Height = physH * _dipScaleY;
        Highlight.Visibility = Visibility.Visible;
    }

    public void HideHighlight() => Highlight.Visibility = Visibility.Collapsed;
}
