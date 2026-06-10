using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using VisNav.App.Interop;

namespace VisNav.App;

/// <summary>
/// Snipping-tool-style region selector. The user drags a box; on release it reports the
/// selected rectangle in PHYSICAL screen pixels via <see cref="RegionSelected"/>, then closes.
/// Excluded from screen capture so the dim backdrop / selection box don't get OCR'd.
/// </summary>
public partial class RegionSelectOverlay : Window
{
    private Point _start;
    private bool _dragging;
    private double _dipScaleX = 1.0;
    private double _dipScaleY = 1.0;

    /// <summary>Selected rectangle in physical screen pixels (x, y, width, height).</summary>
    public event Action<int, int, int, int>? RegionSelected;

    public RegionSelectOverlay()
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
        var m = source.CompositionTarget.TransformFromDevice;
        _dipScaleX = m.M11;
        _dipScaleY = m.M22;

        // Keep the dim backdrop / selection box out of the captured image.
        NativeMethods.ExcludeFromCapture(source.Handle);

        // Take keyboard focus so Esc works even before the user starts dragging.
        Activate();
        Focus();
        Keyboard.Focus(this);

        PositionCancelButton();
    }

    /// <summary>Places the Cancel button at the top-center of the primary screen.</summary>
    private void PositionCancelButton()
    {
        CancelButton.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double w = CancelButton.DesiredSize.Width;
        double primaryCenterX = SystemParameters.PrimaryScreenWidth / 2.0;
        Canvas.SetLeft(CancelButton, primaryCenterX - Left - w / 2.0);
        Canvas.SetTop(CancelButton, -Top + 24);
    }

    private void OnCancelClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true; // don't let this start a region drag
        Close();
    }

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        _start = e.GetPosition(RootCanvas);
        _dragging = true;
        Canvas.SetLeft(SelRect, _start.X);
        Canvas.SetTop(SelRect, _start.Y);
        SelRect.Width = 0;
        SelRect.Height = 0;
        SelRect.Visibility = Visibility.Visible;
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (!_dragging)
            return;
        var p = e.GetPosition(RootCanvas);
        double x = Math.Min(p.X, _start.X);
        double y = Math.Min(p.Y, _start.Y);
        Canvas.SetLeft(SelRect, x);
        Canvas.SetTop(SelRect, y);
        SelRect.Width = Math.Abs(p.X - _start.X);
        SelRect.Height = Math.Abs(p.Y - _start.Y);
    }

    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging)
            return;
        _dragging = false;

        double cx = Canvas.GetLeft(SelRect);
        double cy = Canvas.GetTop(SelRect);
        double cw = SelRect.Width;
        double ch = SelRect.Height;

        Close();

        if (cw < 4 || ch < 4)
            return;

        // Canvas (DIP) -> physical screen pixels.
        int px = (int)Math.Round((cx + Left) / _dipScaleX);
        int py = (int)Math.Round((cy + Top) / _dipScaleY);
        int pw = (int)Math.Round(cw / _dipScaleX);
        int ph = (int)Math.Round(ch / _dipScaleY);
        RegionSelected?.Invoke(px, py, pw, ph);
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            Close();
    }
}
