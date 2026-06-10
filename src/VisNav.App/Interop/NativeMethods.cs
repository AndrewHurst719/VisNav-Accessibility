using System.Runtime.InteropServices;

namespace VisNav.App.Interop;

/// <summary>
/// Minimal Win32 P/Invoke surface for the overlays: reading the global cursor
/// position and making a window click-through / non-activating.
/// </summary>
internal static class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorPos(out POINT lpPoint);

    // Per-thread DPI awareness. DPI_AWARENESS_CONTEXT_UNAWARE = (HANDLE)-1: the OS treats the
    // thread's windows as 96-DPI and bitmap-stretches them per monitor, giving one uniform
    // coordinate space — ideal for a cursor overlay that must align across mixed-DPI monitors.
    public static readonly IntPtr DPI_AWARENESS_CONTEXT_UNAWARE = new(-1);

    [DllImport("user32.dll")]
    public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    // Extended window styles.
    public const int GWL_EXSTYLE = -20;
    public const long WS_EX_TRANSPARENT = 0x00000020; // click-through
    public const long WS_EX_LAYERED = 0x00080000;
    public const long WS_EX_TOOLWINDOW = 0x00000080; // hide from Alt+Tab
    public const long WS_EX_NOACTIVATE = 0x08000000; // never steal focus

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    // Display affinity (SetWindowDisplayAffinity).
    public const uint WDA_NONE = 0x00;
    public const uint WDA_EXCLUDEFROMCAPTURE = 0x11; // Win10 2004+: window shows on screen but not in captures/magnifier

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

    /// <summary>
    /// Adds the layered + transparent + tool-window + no-activate styles so the
    /// window floats above everything and passes all input through to apps below.
    /// </summary>
    public static void MakeClickThrough(IntPtr hWnd)
    {
        var current = GetWindowLongPtr(hWnd, GWL_EXSTYLE).ToInt64();
        var updated = current | WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
        SetWindowLongPtr(hWnd, GWL_EXSTYLE, new IntPtr(updated));
    }

    /// <summary>
    /// Excludes the window from screen capture and the magnifier (it remains visible on
    /// screen). No-op on Windows builds older than 10 2004.
    /// </summary>
    public static void ExcludeFromCapture(IntPtr hWnd) =>
        SetWindowDisplayAffinity(hWnd, WDA_EXCLUDEFROMCAPTURE);
}
