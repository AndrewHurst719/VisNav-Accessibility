using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Hardcodet.Wpf.TaskbarNotification;
using VisNav.Core.Settings;

namespace VisNav.App;

/// <summary>
/// Application entry point. VisNav lives in the system tray: it has no main window,
/// and closing the settings window does not exit the app (only the tray "Exit" does).
/// </summary>
public partial class App : Application
{
    private const string MagnifierChordId = "magnifier";
    private const string NarratorChordId = "narrator";
    private const string ReadRegionChordId = "readregion";
    private const string PauseChordId = "ttspause";
    private const string StopChordId = "ttsstop";

    private TaskbarIcon? _trayIcon;
    private SettingsWindow? _settingsWindow;
    private CrosshairOverlay? _crosshair;
    private MagnifierEngine? _magnifier;
    private HotkeyManager? _hotkeys;
    private bool _magnifierActive;

    private UiaTextScanner? _scanner;
    private NarratorService? _narrator;
    private NarratorOverlay? _readOverlay;
    private OcrService? _ocr;
    private RegionSelectOverlay? _regionOverlay;
    private ReadingHighlightOverlay? _regionHighlight;
    private System.Speech.Synthesis.Prompt? _regionPrompt;

    private SettingsService _settingsService = null!;
    private VisNavSettings _settings = null!;
    private Mutex? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Only one instance may run — multiple would install competing global hooks and
        // fight over the same activation hotkeys.
        _singleInstance = new Mutex(initiallyOwned: true, @"Local\VisNavAccessibility.SingleInstance", out bool isNew);
        if (!isNew)
        {
            _singleInstance.Dispose();
            _singleInstance = null;
            Shutdown();
            return;
        }

        // Tray-resident: don't quit when the last window closes.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _settingsService = new SettingsService();
        _settings = _settingsService.Load();

        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "VisNav Accessibility",
            IconSource = (ImageSource)FindResource("TrayCrosshair"),
            ContextMenu = BuildTrayMenu(),
        };
        _trayIcon.TrayMouseDoubleClick += (_, _) => ShowSettings();

        _crosshair = new CrosshairOverlay();
        ApplyCrosshair();

        _magnifier = new MagnifierEngine();
        _scanner = new UiaTextScanner();
        _narrator = new NarratorService();
        _narrator.Completed += OnSpeechCompleted;
        _ocr = new OcrService();
        _regionHighlight = new ReadingHighlightOverlay();

        _hotkeys = new HotkeyManager();
        _hotkeys.ChordPressed += OnChordPressed;
        _hotkeys.ChordReleased += OnChordReleased;
        _hotkeys.ZoomScrolled += OnZoomScrolled;
        _hotkeys.Install();

        ApplyMagnifier();
        ApplyNarrator();

        // Show the settings window once on launch so first-run users see the app.
        ShowSettings();
    }

    /// <summary>Registers the active feature hotkeys (only enabled features respond).</summary>
    private void RefreshChords()
    {
        if (_hotkeys is null)
            return;

        var chords = new List<(string, IReadOnlyList<int>)>();
        if (_settings.Magnifier.Enabled)
            chords.Add((MagnifierChordId, _settings.Magnifier.ActivationChord));
        if (_settings.Narrator.Enabled)
        {
            chords.Add((NarratorChordId, _settings.Narrator.ScanChord));
            chords.Add((ReadRegionChordId, _settings.Narrator.ReadRegionChord));
            chords.Add((PauseChordId, _settings.Narrator.PauseChord));
            chords.Add((StopChordId, _settings.Narrator.StopChord));
        }
        _hotkeys.SetChords(chords);
        Diagnostics.Log($"RefreshChords: [{string.Join(" | ", chords.Select(c => c.Item1 + ":" + string.Join(",", c.Item2)))}]");
    }

    /// <summary>Pushes magnifier settings to the engine and arms/disarms the hotkey.</summary>
    private void ApplyMagnifier()
    {
        if (_magnifier is null)
            return;

        _magnifier.SetZoom(_settings.Magnifier.ZoomFactor);
        _magnifier.SetLensSize(_settings.Magnifier.LensWidth, _settings.Magnifier.LensHeight);
        RefreshChords();

        // If the feature was switched off, make sure the lens is hidden.
        if (!_settings.Magnifier.Enabled && _magnifierActive)
            SetMagnifierActive(false);
    }

    /// <summary>Pushes narrator settings (voice/rate) and arms/disarms the scan hotkey.</summary>
    private void ApplyNarrator()
    {
        _narrator?.Configure(_settings.Narrator.VoiceName, _settings.Narrator.SpeechRate);
        RefreshChords();

        if (!_settings.Narrator.Enabled)
            _readOverlay?.Close();
    }

    private void OnChordPressed(string id)
    {
        switch (id)
        {
            case MagnifierChordId when _settings.Magnifier.Enabled:
                if (_settings.Magnifier.Activation == MagnifierActivation.Hold)
                    SetMagnifierActive(true);
                else
                    SetMagnifierActive(!_magnifierActive);
                break;

            case NarratorChordId when _settings.Narrator.Enabled:
                OnNarratorScan();
                break;

            case ReadRegionChordId when _settings.Narrator.Enabled:
                StartRegionRead();
                break;

            case PauseChordId when _settings.Narrator.Enabled:
                _narrator?.TogglePause();
                break;

            case StopChordId when _settings.Narrator.Enabled:
                _narrator?.Stop();
                _regionHighlight?.HideHighlight();
                break;
        }
    }

    /// <summary>Hides the region reading highlight when its utterance finishes (or is superseded).</summary>
    private void OnSpeechCompleted(System.Speech.Synthesis.Prompt prompt)
    {
        if (prompt == _regionPrompt)
            Dispatcher.Invoke(() => _regionHighlight?.HideHighlight());
    }

    private void OnChordReleased(string id)
    {
        if (id == MagnifierChordId && _settings.Magnifier.Activation == MagnifierActivation.Hold)
            SetMagnifierActive(false);
    }

    /// <summary>Scans the foreground window for text and shows the Read-button overlay.</summary>
    private void OnNarratorScan()
    {
        // Toggle: a second press dismisses the overlay.
        if (_readOverlay is not null)
        {
            _readOverlay.Close();
            return;
        }

        // Capture the user's window now (before our overlay can take focus).
        var hwnd = Interop.NativeMethods.GetForegroundWindow();
        Diagnostics.Log($"narrator scan triggered hwnd={hwnd}");
        var dispatcher = Dispatcher;
        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                var blocks = _scanner!.Scan(hwnd, _settings.Narrator.GroupingIntensity);
                dispatcher.Invoke(() => ShowReadOverlay(blocks));
            }
            catch (Exception ex)
            {
                Diagnostics.Log($"narrator scan EXCEPTION: {ex}");
            }
        });
    }

    private void ShowReadOverlay(IReadOnlyList<TextGroup> blocks)
    {
        if (_readOverlay is not null)
            return;
        if (blocks.Count == 0)
        {
            Diagnostics.Log("narrator: no readable text found in foreground window");
            return;
        }

        _readOverlay = new NarratorOverlay(blocks, _narrator!);
        _readOverlay.Closed += (_, _) => _readOverlay = null;
        _readOverlay.Show();
    }

    /// <summary>Shows the region selector; the chosen box is OCR'd and read aloud.</summary>
    private void StartRegionRead()
    {
        if (_regionOverlay is not null)
            return;
        _regionOverlay = new RegionSelectOverlay();
        _regionOverlay.RegionSelected += OnRegionSelected;
        _regionOverlay.Closed += (_, _) => _regionOverlay = null;
        _regionOverlay.Show();
    }

    private async void OnRegionSelected(int x, int y, int w, int h)
    {
        if (_ocr is null || _narrator is null)
            return;
        if (!_ocr.Available)
        {
            _narrator.Speak("Text recognition is not available on this PC.");
            return;
        }

        // Let the selector window fully clear the screen before capturing.
        await System.Threading.Tasks.Task.Delay(80);

        string text;
        try
        {
            text = await _ocr.ReadRegionAsync(x, y, w, h);
        }
        catch (Exception ex)
        {
            Diagnostics.Log($"OCR error: {ex.Message}");
            _narrator.Speak("Could not read that area.");
            return;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            _regionPrompt = _narrator.Speak("No text found.");
            return;
        }

        // Keep the light-blue frame on the cropped area while it's read; it clears when the
        // utterance finishes or is superseded (see OnSpeechCompleted). Speak() cancels any
        // in-progress speech, so the most recent request always wins.
        _regionHighlight?.ShowAt(x, y, w, h);
        _regionPrompt = _narrator.Speak(text);
    }

    private void SetMagnifierActive(bool active)
    {
        if (_magnifier is null || _hotkeys is null)
            return;

        _magnifierActive = active;
        _hotkeys.MagnifierActive = active;

        if (active)
            _magnifier.Start();
        else
            _magnifier.Stop();
    }

    private void OnZoomScrolled(int direction)
    {
        if (_magnifier is null || !_magnifierActive)
            return;

        double factor = direction > 0 ? 1.2 : 1.0 / 1.2;
        double zoom = Math.Clamp(_settings.Magnifier.ZoomFactor * factor, 1.0, 16.0);
        _settings.Magnifier.ZoomFactor = Math.Round(zoom, 2);
        _magnifier.SetZoom(_settings.Magnifier.ZoomFactor);
    }

    /// <summary>Pushes the current crosshair settings to the overlay and shows/hides it.</summary>
    private void ApplyCrosshair()
    {
        if (_crosshair is null)
            return;

        _crosshair.Apply(_settings.Crosshair);

        if (_settings.Crosshair.Enabled)
            _crosshair.Start();
        else
            _crosshair.Stop();
    }

    private ContextMenu BuildTrayMenu()
    {
        var menu = new ContextMenu();

        var open = new MenuItem { Header = MenuLabel("Open Settings") };
        open.Click += (_, _) => ShowSettings();

        var readRegion = new MenuItem { Header = MenuLabel("Read a region (OCR)") };
        readRegion.Click += (_, _) => StartRegionRead();

        var exit = new MenuItem { Header = MenuLabel("Exit") };
        exit.Click += (_, _) => Shutdown();

        menu.Items.Add(open);
        menu.Items.Add(readRegion);
        menu.Items.Add(new Separator());
        menu.Items.Add(exit);
        return menu;
    }

    // Explicit black-on-the-default-light-menu text. Without an explicit Foreground the
    // app-wide near-white TextBlock style bleeds into the tray menu (white-on-white).
    private static TextBlock MenuLabel(string text) => new()
    {
        Text = text,
        Foreground = System.Windows.Media.Brushes.Black,
        FontSize = 14,
    };

    private void ShowSettings()
    {
        if (_settingsWindow is null)
        {
            _settingsWindow = new SettingsWindow(_settingsService, _settings, _hotkeys!, _narrator!);
            _settingsWindow.CrosshairChanged += ApplyCrosshair;
            _settingsWindow.MagnifierChanged += ApplyMagnifier;
            _settingsWindow.NarratorChanged += ApplyNarrator;
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
        }

        _settingsWindow.WindowState = WindowState.Normal;
        _settingsWindow.Activate();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Persist runtime changes (e.g. scroll-wheel zoom) that never went through the window.
        _settingsService?.Save(_settings);

        _readOverlay?.Close();
        _regionOverlay?.Close();
        _regionHighlight?.Close();
        _crosshair?.Stop();
        _crosshair?.Close();
        _magnifier?.Dispose();
        _narrator?.Dispose();
        _hotkeys?.Dispose();
        _trayIcon?.Dispose();
        _singleInstance?.ReleaseMutex();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
