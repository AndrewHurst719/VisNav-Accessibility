using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using VisNav.App.Interop;
using VisNav.Core.Settings;

namespace VisNav.App;

/// <summary>
/// A full-virtual-screen, transparent, click-through overlay that draws a thin
/// circle centered on the mouse cursor and follows it every frame.
/// </summary>
public partial class CrosshairOverlay : Window
{
    private CrosshairSettings _settings = new();
    private bool _rendering;

    // Device-pixels -> DIP scale (filled once the HWND exists). Identity until then.
    private double _dipScaleX = 1.0;
    private double _dipScaleY = 1.0;

    public CrosshairOverlay()
    {
        InitializeComponent();

        // Cover every monitor (virtual screen), in DIPs.
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

        // Keep the crosshair out of the magnifier lens (and other screen captures);
        // it still renders normally on screen.
        NativeMethods.ExcludeFromCapture(source.Handle);

        // device px -> DIP: multiply by these (TransformFromDevice is 1/dpiScale).
        var m = source.CompositionTarget.TransformFromDevice;
        _dipScaleX = m.M11;
        _dipScaleY = m.M22;

    }

    /// <summary>Applies color / radius / thickness to the ring visuals.</summary>
    public void Apply(CrosshairSettings settings)
    {
        _settings = settings;

        var stroke = CrosshairPalette.Stroke(settings.Color);
        double diameter = settings.OuterRadius * 2.0;
        double opacity = Math.Clamp(settings.OpacityPercent, 10, 100) / 100.0;

        Ring.Width = diameter;
        Ring.Height = diameter;
        Ring.Stroke = new SolidColorBrush(stroke);
        Ring.StrokeThickness = settings.OuterThickness;
        Ring.Opacity = opacity;
        OutlineRing.Opacity = opacity;

        if (CrosshairPalette.HasOutline(settings.Color))
        {
            OutlineRing.Width = diameter;
            OutlineRing.Height = diameter;
            OutlineRing.Stroke = new SolidColorBrush(CrosshairPalette.OutlineColor);
            // Outline extends ~2 DIP beyond the ring on each side.
            OutlineRing.StrokeThickness = settings.OuterThickness + 4;
            OutlineRing.Visibility = Visibility.Visible;
        }
        else
        {
            OutlineRing.Visibility = Visibility.Collapsed;
        }

        UpdatePosition();
    }

    /// <summary>Shows the overlay and begins tracking the cursor.</summary>
    public void Start()
    {
        if (!IsVisible)
            Show();

        if (!_rendering)
        {
            CompositionTarget.Rendering += OnRendering;
            _rendering = true;
        }

        UpdatePosition();
    }

    /// <summary>Stops tracking and hides the overlay.</summary>
    public void Stop()
    {
        if (_rendering)
        {
            CompositionTarget.Rendering -= OnRendering;
            _rendering = false;
        }

        Hide();
    }

    private void OnRendering(object? sender, EventArgs e) => UpdatePosition();

    private void UpdatePosition()
    {
        if (!NativeMethods.GetCursorPos(out var p))
            return;

        // Physical cursor px -> DIP, then into this window's canvas space. When this overlay
        // runs on a DPI-unaware thread, both the cursor and the window are in one virtualized
        // coordinate space (scale 1) and Windows handles per-monitor stretching, so this is
        // correct across mixed-DPI monitors.
        double cx = p.X * _dipScaleX - Left;
        double cy = p.Y * _dipScaleY - Top;

        double radius = _settings.OuterRadius;
        PositionRing(Ring, cx, cy, radius);
        if (OutlineRing.Visibility == Visibility.Visible)
            PositionRing(OutlineRing, cx, cy, radius);
    }

    private static void PositionRing(System.Windows.Shapes.Ellipse ring, double cx, double cy, double radius)
    {
        Canvas.SetLeft(ring, cx - radius);
        Canvas.SetTop(ring, cy - radius);
    }
}
