using System.Windows;
using System.Windows.Threading;
using VisNav.App.Interop;
using static VisNav.App.Interop.MagnifierNative;

namespace VisNav.App;

/// <summary>
/// Drives a Windows Magnification-API lens: a topmost, click-through host window
/// containing a magnifier control that shows a magnified, cursor-centered view of
/// the screen and follows the pointer. All coordinates are physical pixels.
/// </summary>
public sealed class MagnifierEngine : IDisposable
{
    private const string HostClassName = "VisNavMagnifierHost";

    private readonly DispatcherTimer _timer;
    private WndProc? _wndProc;   // kept alive to avoid GC of the native callback
    private bool _classRegistered;
    private bool _magInitialized;

    private IntPtr _hostHwnd;
    private IntPtr _magHwnd;
    private bool _running;

    private double _zoom = 2.0;
    private int _lensW = 480;
    private int _lensH = 320;

    public MagnifierEngine()
    {
        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16),
        };
        _timer.Tick += (_, _) => UpdateFrame();
    }

    public bool IsRunning => _running;

    public void SetZoom(double zoom)
    {
        _zoom = Math.Clamp(zoom, 1.0, 32.0);
        ApplyTransform();
    }

    public void SetLensSize(int width, int height)
    {
        _lensW = Math.Max(80, width);
        _lensH = Math.Max(60, height);
    }

    /// <summary>Creates resources if needed, shows the lens, and starts following the cursor.</summary>
    public void Start()
    {
        Diagnostics.Log($"MagnifierEngine.Start running={_running}");
        if (_running)
            return;

        if (!EnsureCreated())
        {
            Diagnostics.Log("EnsureCreated returned false");
            return;
        }

        ApplyTransform();
        UpdateFrame();
        ShowWindow(_hostHwnd, SW_SHOWNOACTIVATE);
        _timer.Start();
        _running = true;
    }

    /// <summary>Hides the lens and stops following the cursor.</summary>
    public void Stop()
    {
        if (!_running)
            return;

        _timer.Stop();
        if (_hostHwnd != IntPtr.Zero)
            ShowWindow(_hostHwnd, SW_HIDE);
        _running = false;
    }

    private bool EnsureCreated()
    {
        var hInstance = GetModuleHandle(null);

        if (!_magInitialized)
        {
            if (!MagInitialize())
            {
                Diagnostics.Log($"MagInitialize FAILED err={System.Runtime.InteropServices.Marshal.GetLastWin32Error()}");
                return false;
            }
            _magInitialized = true;
            Diagnostics.Log("MagInitialize ok");
        }

        if (!_classRegistered)
        {
            _wndProc = DefWindowProc;
            var wc = new WNDCLASSEX
            {
                cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<WNDCLASSEX>(),
                lpfnWndProc = _wndProc,
                hInstance = hInstance,
                lpszClassName = HostClassName,
            };
            if (RegisterClassEx(ref wc) == 0)
            {
                Diagnostics.Log($"RegisterClassEx FAILED err={System.Runtime.InteropServices.Marshal.GetLastWin32Error()}");
                return false;
            }
            _classRegistered = true;
            Diagnostics.Log("RegisterClassEx ok");
        }

        if (_hostHwnd == IntPtr.Zero)
        {
            int exStyle = WS_EX_TOPMOST | WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            _hostHwnd = CreateWindowEx(
                exStyle, HostClassName, "VisNav Magnifier", WS_POPUP,
                0, 0, _lensW, _lensH,
                IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);
            if (_hostHwnd == IntPtr.Zero)
            {
                Diagnostics.Log($"CreateWindowEx host FAILED err={System.Runtime.InteropServices.Marshal.GetLastWin32Error()}");
                return false;
            }
            SetLayeredWindowAttributes(_hostHwnd, 0, 255, LWA_ALPHA);
            Diagnostics.Log($"host hwnd={_hostHwnd}");
        }

        if (_magHwnd == IntPtr.Zero)
        {
            _magHwnd = CreateWindowEx(
                0, WC_MAGNIFIER, "VisNav Magnifier Control",
                WS_CHILD | WS_VISIBLE | MS_SHOWMAGNIFIEDCURSOR,
                0, 0, _lensW, _lensH,
                _hostHwnd, IntPtr.Zero, hInstance, IntPtr.Zero);
            if (_magHwnd == IntPtr.Zero)
            {
                Diagnostics.Log($"CreateWindowEx magnifier FAILED err={System.Runtime.InteropServices.Marshal.GetLastWin32Error()}");
                return false;
            }
            Diagnostics.Log($"mag hwnd={_magHwnd}");
        }

        return true;
    }

    private void ApplyTransform()
    {
        if (_magHwnd == IntPtr.Zero)
            return;
        var t = MAGTRANSFORM.ForScale((float)_zoom);
        MagSetWindowTransform(_magHwnd, ref t);
    }

    private void UpdateFrame()
    {
        if (_hostHwnd == IntPtr.Zero || _magHwnd == IntPtr.Zero)
            return;
        if (!NativeMethods.GetCursorPos(out var cursor))
            return;

        int vsX = (int)SystemParameters.VirtualScreenLeft;
        int vsY = (int)SystemParameters.VirtualScreenTop;
        int vsW = (int)SystemParameters.VirtualScreenWidth;
        int vsH = (int)SystemParameters.VirtualScreenHeight;

        // The lens floats up-and-to-the-right of the pointer: its bottom-left corner sits
        // at the cursor tip. Flip below / to the left near screen edges so it stays visible.
        int hostX = cursor.X;
        int hostY = cursor.Y - _lensH;
        if (hostY < vsY) hostY = cursor.Y;                          // would clip top → drop below
        if (hostX + _lensW > vsX + vsW) hostX = cursor.X - _lensW;  // would clip right → go left
        hostX = Math.Clamp(hostX, vsX, Math.Max(vsX, vsX + vsW - _lensW));
        hostY = Math.Clamp(hostY, vsY, Math.Max(vsY, vsY + vsH - _lensH));

        MoveWindow(_hostHwnd, hostX, hostY, _lensW, _lensH, true);
        SetWindowPos(_magHwnd, IntPtr.Zero, 0, 0, _lensW, _lensH, SWP_NOACTIVATE | SWP_NOZORDER);

        // Source rect: the screen area to magnify, centered on the cursor, clamped to
        // the virtual screen so edges still show real content.
        int srcW = (int)Math.Round(_lensW / _zoom);
        int srcH = (int)Math.Round(_lensH / _zoom);

        int srcLeft = cursor.X - srcW / 2;
        int srcTop = cursor.Y - srcH / 2;
        srcLeft = Math.Clamp(srcLeft, vsX, Math.Max(vsX, vsX + vsW - srcW));
        srcTop = Math.Clamp(srcTop, vsY, Math.Max(vsY, vsY + vsH - srcH));

        var rect = new RECT { left = srcLeft, top = srcTop, right = srcLeft + srcW, bottom = srcTop + srcH };
        MagSetWindowSource(_magHwnd, rect);
    }

    public void Dispose()
    {
        Stop();
        if (_magHwnd != IntPtr.Zero) { DestroyWindow(_magHwnd); _magHwnd = IntPtr.Zero; }
        if (_hostHwnd != IntPtr.Zero) { DestroyWindow(_hostHwnd); _hostHwnd = IntPtr.Zero; }
        if (_magInitialized) { MagUninitialize(); _magInitialized = false; }
    }
}
