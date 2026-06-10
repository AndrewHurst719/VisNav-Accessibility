using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using VisNav.Core.Settings;

namespace VisNav.App;

/// <summary>
/// High-contrast settings window. Controls are two-way bound to the live
/// <see cref="VisNavSettings"/> instance; the file is written when the window closes.
/// <see cref="CrosshairChanged"/> and <see cref="MagnifierChanged"/> fire on edits so the
/// overlays update in real time.
/// </summary>
public partial class SettingsWindow : Window
{
    // Lens size limits (physical px) and the diagram's preview scale.
    private const int LensMinW = 80, LensMaxW = 1200;
    private const int LensMinH = 60, LensMaxH = 800;
    private const double Scale = 0.25; // 300px preview / 1200px max

    private readonly SettingsService _settingsService;
    private readonly VisNavSettings _settings;
    private readonly HotkeyManager _hotkeys;
    private readonly NarratorService _narrator;
    private readonly Dictionary<CrosshairColor, Border> _swatches = new();
    private bool _initialized;

    /// <summary>Raised when any crosshair setting changes (toggle, color, radius, thickness).</summary>
    public event Action? CrosshairChanged;

    /// <summary>Raised when any magnifier setting changes (toggle, hotkey, zoom, lens size).</summary>
    public event Action? MagnifierChanged;

    /// <summary>Raised when any narrator setting changes (toggle, hotkey, voice, rate).</summary>
    public event Action? NarratorChanged;

    public SettingsWindow(SettingsService settingsService, VisNavSettings settings, HotkeyManager hotkeys, NarratorService narrator)
    {
        _settingsService = settingsService;
        _settings = settings;
        _hotkeys = hotkeys;
        _narrator = narrator;

        InitializeComponent();
        DataContext = _settings;

        BuildColorSwatches();
        HotkeyText.Text = ChordFormatter.Describe(_settings.Magnifier.ActivationChord);
        UpdateDiagram();
        PopulateVoices();
        ScanHotkeyText.Text = ChordFormatter.Describe(_settings.Narrator.ScanChord);
        RegionHotkeyText.Text = ChordFormatter.Describe(_settings.Narrator.ReadRegionChord);
        PauseHotkeyText.Text = ChordFormatter.Describe(_settings.Narrator.PauseChord);
        StopHotkeyText.Text = ChordFormatter.Describe(_settings.Narrator.StopChord);
        _initialized = true;
    }

    // ----- Crosshair -----

    private void BuildColorSwatches()
    {
        foreach (var color in CrosshairPalette.Order)
        {
            var preview = new Ellipse
            {
                Width = 34,
                Height = 34,
                Stroke = new SolidColorBrush(CrosshairPalette.Stroke(color)),
                StrokeThickness = 4,
                Fill = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            var label = new TextBlock
            {
                Text = CrosshairPalette.DisplayName(color),
                FontSize = 14,
                Foreground = (Brush)FindResource("FgBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.NoWrap,
            };

            var stack = new StackPanel { Margin = new Thickness(6) };
            stack.Children.Add(preview);
            stack.Children.Add(label);

            var tile = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x10, 0x10, 0x10)),
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(3),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 0, 8, 8),
                Cursor = System.Windows.Input.Cursors.Hand,
                Child = stack,
                Tag = color,
                Focusable = true,
            };
            tile.MouseLeftButtonUp += (_, _) => SelectColor(color);
            tile.KeyDown += (_, e) =>
            {
                if (e.Key is System.Windows.Input.Key.Enter or System.Windows.Input.Key.Space)
                    SelectColor(color);
            };

            _swatches[color] = tile;
            ColorSwatchPanel.Children.Add(tile);
        }

        HighlightSelectedSwatch();
    }

    private void SelectColor(CrosshairColor color)
    {
        _settings.Crosshair.Color = color;
        HighlightSelectedSwatch();
        CrosshairChanged?.Invoke();
    }

    private void HighlightSelectedSwatch()
    {
        var accent = (Brush)FindResource("AccentBrush");
        foreach (var (color, tile) in _swatches)
            tile.BorderBrush = color == _settings.Crosshair.Color ? accent : Brushes.Transparent;
    }

    private void OnCrosshairChanged(object sender, RoutedEventArgs e) => CrosshairChanged?.Invoke();

    private void OnCrosshairValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        => CrosshairChanged?.Invoke();

    // ----- Magnifier -----

    private void OnMagnifierChanged(object sender, RoutedEventArgs e) => MagnifierChanged?.Invoke();

    private void OnMagnifierValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        => MagnifierChanged?.Invoke();

    private void OnChangeHotkey(object sender, RoutedEventArgs e)
    {
        HotkeyText.Text = "Press your combination…  (Esc to cancel)";
        HotkeyBox.BorderBrush = (Brush)FindResource("AccentBrush");

        _hotkeys.BeginCapture(result =>
        {
            HotkeyBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A));

            if (result is { Count: > 0 })
            {
                _settings.Magnifier.ActivationChord = result.ToList();
                MagnifierChanged?.Invoke(); // App.ApplyMagnifier re-registers the chord
            }

            HotkeyText.Text = ChordFormatter.Describe(_settings.Magnifier.ActivationChord);
        });
    }

    private void OnCornerDrag(object sender, DragDeltaEventArgs e)
    {
        double w = Math.Clamp(LensRect.Width + e.HorizontalChange, LensMinW * Scale, LensMaxW * Scale);
        double h = Math.Clamp(LensRect.Height + e.VerticalChange, LensMinH * Scale, LensMaxH * Scale);

        _settings.Magnifier.LensWidth = (int)Math.Round(w / Scale);
        _settings.Magnifier.LensHeight = (int)Math.Round(h / Scale);

        UpdateDiagram();
        MagnifierChanged?.Invoke();
    }

    /// <summary>Lays out the lens rectangle (anchored top-left) and corner handle from settings.</summary>
    private void UpdateDiagram()
    {
        double w = Math.Clamp(_settings.Magnifier.LensWidth, LensMinW, LensMaxW) * Scale;
        double h = Math.Clamp(_settings.Magnifier.LensHeight, LensMinH, LensMaxH) * Scale;

        LensRect.Width = w;
        LensRect.Height = h;
        Canvas.SetLeft(LensRect, 0);
        Canvas.SetTop(LensRect, 0);

        Canvas.SetLeft(CornerThumb, w - CornerThumb.Width / 2);
        Canvas.SetTop(CornerThumb, h - CornerThumb.Height / 2);

        LensSizeText.Text = $"{_settings.Magnifier.LensWidth} × {_settings.Magnifier.LensHeight} px";
    }

    // ----- Narrator -----

    private void PopulateVoices()
    {
        var voices = _narrator.InstalledVoices();
        VoiceCombo.ItemsSource = voices;
        if (voices.Count == 0)
        {
            VoiceCombo.IsEnabled = false;
            return;
        }

        var current = _settings.Narrator.VoiceName;
        VoiceCombo.SelectedItem = current is not null && voices.Contains(current) ? current : voices[0];
    }

    private void OnVoiceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized)
            return;
        _settings.Narrator.VoiceName = VoiceCombo.SelectedItem as string;
        NarratorChanged?.Invoke();
    }

    private void OnNarratorChanged(object sender, RoutedEventArgs e)
    {
        if (_initialized)
            NarratorChanged?.Invoke();
    }

    private void OnNarratorValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_initialized)
            NarratorChanged?.Invoke();
    }

    private void OnChangeScanHotkey(object sender, RoutedEventArgs e)
    {
        ScanHotkeyText.Text = "Press your combination…  (Esc to cancel)";
        ScanHotkeyBox.BorderBrush = (Brush)FindResource("AccentBrush");

        _hotkeys.BeginCapture(result =>
        {
            ScanHotkeyBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A));

            if (result is { Count: > 0 })
            {
                _settings.Narrator.ScanChord = result.ToList();
                NarratorChanged?.Invoke();
            }

            ScanHotkeyText.Text = ChordFormatter.Describe(_settings.Narrator.ScanChord);
        });
    }

    private void OnChangeRegionHotkey(object sender, RoutedEventArgs e)
    {
        RegionHotkeyText.Text = "Press your combination…  (Esc to cancel)";
        RegionHotkeyBox.BorderBrush = (Brush)FindResource("AccentBrush");

        _hotkeys.BeginCapture(result =>
        {
            RegionHotkeyBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A));

            if (result is { Count: > 0 })
            {
                _settings.Narrator.ReadRegionChord = result.ToList();
                NarratorChanged?.Invoke();
            }

            RegionHotkeyText.Text = ChordFormatter.Describe(_settings.Narrator.ReadRegionChord);
        });
    }

    private void OnChangePauseHotkey(object sender, RoutedEventArgs e) =>
        CaptureNarratorChord(PauseHotkeyBox, PauseHotkeyText,
            list => _settings.Narrator.PauseChord = list, () => _settings.Narrator.PauseChord);

    private void OnChangeStopHotkey(object sender, RoutedEventArgs e) =>
        CaptureNarratorChord(StopHotkeyBox, StopHotkeyText,
            list => _settings.Narrator.StopChord = list, () => _settings.Narrator.StopChord);

    private void CaptureNarratorChord(Border box, TextBlock label, Action<List<int>> set, Func<List<int>> get)
    {
        label.Text = "Press your combination…  (Esc to cancel)";
        box.BorderBrush = (Brush)FindResource("AccentBrush");

        _hotkeys.BeginCapture(result =>
        {
            box.BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A));
            if (result is { Count: > 0 })
            {
                set(result.ToList());
                NarratorChanged?.Invoke();
            }
            label.Text = ChordFormatter.Describe(get());
        });
    }

    // ----- Window -----

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosing(CancelEventArgs e)
    {
        // Persist on close. The app keeps running in the tray afterward.
        _settingsService.Save(_settings);
        base.OnClosing(e);
    }
}
