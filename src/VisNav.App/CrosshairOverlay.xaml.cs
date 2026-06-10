using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using VisNav.App.Interop;
using VisNav.Core.Settings;

namespace VisNav.App;

/// <summary>
/// A small, transparent, click-through window that follows the cursor and draws a thin ring
/// centered on the pointer. The whole app is Per-Monitor-V2 DPI aware, so this window adopts
/// the scale of whatever monitor the cursor is on — the ring stays correctly sized and
/// centered across monitors with different display scaling.
/// </summary>
public partial class CrosshairOverlay : Window
{
    private const double SizePadding = 12; // DIP room for stroke + outline + anti-aliasing

    private readonly DispatcherTimer _timer;
    private CrosshairSettings _settings = new();
    private IntPtr _handle;
    private bool _running;

    public CrosshairOverlay()
    {
        InitializeComponent();
        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16),
        };
        _timer.Tick += (_, _) => Follow();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var source = (HwndSource)PresentationSource.FromVisual(this)!;
        _handle = source.Handle;
        NativeMethods.MakeClickThrough(_handle);
        NativeMethods.ExcludeFromCapture(_handle); // keep it out of the magnifier lens / screenshots
    }

    /// <summary>Applies color / radius / thickness / opacity and sizes the window to the ring.</summary>
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

        if (CrosshairPalette.HasOutline(settings.Color))
        {
            OutlineRing.Width = diameter;
            OutlineRing.Height = diameter;
            OutlineRing.Stroke = new SolidColorBrush(CrosshairPalette.OutlineColor);
            OutlineRing.StrokeThickness = settings.OuterThickness + 4;
            OutlineRing.Opacity = opacity;
            OutlineRing.Visibility = Visibility.Visible;
        }
        else
        {
            OutlineRing.Visibility = Visibility.Collapsed;
        }

        // Window just big enough to hold the ring (DIP). It renders at the current monitor's scale.
        double side = diameter + settings.OuterThickness + SizePadding;
        Width = side;
        Height = side;
    }

    public void Start()
    {
        if (!IsVisible)
            Show();
        if (!_running)
        {
            _timer.Start();
            _running = true;
        }
        Follow();
    }

    public void Stop()
    {
        if (_running)
        {
            _timer.Stop();
            _running = false;
        }
        Hide();
    }

    private void Follow()
    {
        if (_handle == IntPtr.Zero || !NativeMethods.GetCursorPos(out var p))
            return;

        // Center the window (physical px) on the cursor. GetWindowRect gives the window's
        // current physical size, which already reflects the monitor's scale under PMv2.
        if (!NativeMethods.GetWindowRect(_handle, out var r))
            return;
        int w = r.Right - r.Left;
        int h = r.Bottom - r.Top;

        NativeMethods.SetWindowPos(_handle, NativeMethods.HWND_TOPMOST,
            p.X - w / 2, p.Y - h / 2, 0, 0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }
}
