using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Speech.Synthesis;
using System.Windows.Interop;
using System.Windows.Media;

namespace VisNav.App;

/// <summary>
/// Narrator mode overlay. After a scan, the screen dims slightly and every detected text
/// block gets a faint outline. The block under the cursor highlights; clicking anywhere in
/// it reads it aloud (no separate button — the highlighted area is the target). The block
/// being read stays highlighted. Esc / right-click / click on empty space dismisses.
/// </summary>
public partial class NarratorOverlay : Window
{
    private sealed class Hit
    {
        public required TextGroup Block;
        public required Rect CanvasRect; // DIP, within this window's canvas
    }

    private readonly IReadOnlyList<TextGroup> _blocks;
    private readonly NarratorService _narrator;
    private readonly List<Hit> _hits = new();

    private Border _hoverHighlight = null!;
    private Border _readingHighlight = null!;
    private Hit? _reading;
    private Prompt? _readingPrompt;

    private double _dipScaleX = 1.0;
    private double _dipScaleY = 1.0;

    public NarratorOverlay(IReadOnlyList<TextGroup> blocks, NarratorService narrator)
    {
        _blocks = blocks;
        _narrator = narrator;

        InitializeComponent();

        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        _narrator.Completed += OnSpeakCompleted;
        Closed += (_, _) =>
        {
            _narrator.Completed -= OnSpeakCompleted;
            _narrator.Stop();
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var source = (HwndSource)PresentationSource.FromVisual(this)!;
        var m = source.CompositionTarget.TransformFromDevice; // device px -> DIP
        _dipScaleX = m.M11;
        _dipScaleY = m.M22;

        BuildVisuals();
        Activate(); // take focus so Esc works
    }

    private void BuildVisuals()
    {
        var accent = ((SolidColorBrush)FindResource("AccentBrush")).Color;

        // No persistent outlines — only the block under the cursor highlights. We just
        // record each block's on-screen rectangle for hit-testing.
        foreach (var block in _blocks)
        {
            var rect = new Rect(
                block.Bounds.X * _dipScaleX - Left,
                block.Bounds.Y * _dipScaleY - Top,
                block.Bounds.Width * _dipScaleX,
                block.Bounds.Height * _dipScaleY);
            _hits.Add(new Hit { Block = block, CanvasRect = rect });
        }

        _readingHighlight = MakeHighlight(Color.FromArgb(0x55, accent.R, accent.G, accent.B), accent);
        _hoverHighlight = MakeHighlight(Color.FromArgb(0x33, accent.R, accent.G, accent.B), accent);
        RootCanvas.Children.Add(_readingHighlight);
        RootCanvas.Children.Add(_hoverHighlight);
    }

    private static Border MakeHighlight(Color fill, Color border) => new()
    {
        BorderBrush = new SolidColorBrush(border),
        BorderThickness = new Thickness(3),
        CornerRadius = new CornerRadius(3),
        Background = new SolidColorBrush(fill),
        IsHitTestVisible = false,
        Visibility = Visibility.Collapsed,
    };

    private static void PlaceHighlight(Border highlight, Rect rect)
    {
        highlight.Width = rect.Width;
        highlight.Height = rect.Height;
        Canvas.SetLeft(highlight, rect.X);
        Canvas.SetTop(highlight, rect.Y);
        highlight.Visibility = Visibility.Visible;
    }

    /// <summary>Smallest block containing the point (most specific when nested).</summary>
    private Hit? HitTest(Point p)
    {
        Hit? best = null;
        double bestArea = double.MaxValue;
        foreach (var hit in _hits)
        {
            if (hit.CanvasRect.Contains(p))
            {
                double area = hit.CanvasRect.Width * hit.CanvasRect.Height;
                if (area < bestArea)
                {
                    bestArea = area;
                    best = hit;
                }
            }
        }
        return best;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        var hit = HitTest(e.GetPosition(RootCanvas));
        if (hit is null)
            _hoverHighlight.Visibility = Visibility.Collapsed;
        else
            PlaceHighlight(_hoverHighlight, hit.CanvasRect);
    }

    private void OnClick(object sender, MouseButtonEventArgs e)
    {
        var hit = HitTest(e.GetPosition(RootCanvas));
        if (hit is null)
        {
            Close(); // clicked empty space
            return;
        }

        if (_reading == hit)
        {
            _narrator.Stop();
            ClearReading();
            return;
        }

        _reading = hit;
        PlaceHighlight(_readingHighlight, hit.CanvasRect);
        _readingPrompt = _narrator.Speak(hit.Block.Text);
    }

    private void ClearReading()
    {
        _reading = null;
        _readingPrompt = null;
        _readingHighlight.Visibility = Visibility.Collapsed;
    }

    private void OnSpeakCompleted(Prompt prompt)
    {
        // Only clear when the utterance we started finishes (or gets superseded).
        if (prompt == _readingPrompt)
            Dispatcher.Invoke(ClearReading);
    }

    private void OnDismiss(object sender, MouseButtonEventArgs e) => Close();

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            Close();
    }
}
