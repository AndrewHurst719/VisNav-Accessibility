using System.Threading;
using System.Windows.Threading;
using VisNav.App.Interop;
using VisNav.Core.Settings;

namespace VisNav.App;

/// <summary>
/// Hosts the crosshair overlay on a dedicated DPI-<b>unaware</b> UI thread. A DPI-unaware
/// window is bitmap-stretched per monitor by Windows and reports the cursor in a single
/// virtualized coordinate space, so the ring stays centered on the pointer across monitors
/// with different scaling (e.g. a 250% laptop screen plus 100% external monitors). Keeping it
/// on its own thread isolates this DPI context from the rest of the (DPI-aware) app.
/// </summary>
public sealed class CrosshairHost : IDisposable
{
    private readonly ManualResetEventSlim _ready = new(false);
    private Thread? _thread;
    private Dispatcher? _dispatcher;
    private CrosshairOverlay? _overlay;

    public void Start()
    {
        _thread = new Thread(ThreadProc)
        {
            IsBackground = true,
            Name = "VisNav Crosshair",
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        _ready.Wait();
    }

    private void ThreadProc()
    {
        // Everything created on this thread — the window, GetCursorPos, SystemParameters —
        // then shares the unaware (virtualized) coordinate space.
        NativeMethods.SetThreadDpiAwarenessContext(NativeMethods.DPI_AWARENESS_CONTEXT_UNAWARE);

        _overlay = new CrosshairOverlay();
        _dispatcher = Dispatcher.CurrentDispatcher;
        _ready.Set();
        Dispatcher.Run();
    }

    /// <summary>Applies settings and shows/hides the overlay (marshaled to the overlay thread).</summary>
    public void Apply(CrosshairSettings settings)
    {
        _dispatcher?.InvokeAsync(() =>
        {
            if (_overlay is null)
                return;
            _overlay.Apply(settings);
            if (settings.Enabled)
                _overlay.Start();
            else
                _overlay.Stop();
        });
    }

    public void Dispose()
    {
        var d = _dispatcher;
        if (d is not null)
        {
            // Non-blocking: queue the close, then the shutdown, so the app's exit never hangs.
            d.InvokeAsync(() =>
            {
                _overlay?.Stop();
                _overlay?.Close();
            });
            d.BeginInvokeShutdown(DispatcherPriority.Normal);
        }
        _thread?.Join(TimeSpan.FromSeconds(2));
    }
}
