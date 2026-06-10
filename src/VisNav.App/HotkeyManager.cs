using VisNav.App.Interop;
using static VisNav.App.Interop.HookNative;

namespace VisNav.App;

/// <summary>
/// Installs global low-level keyboard and mouse hooks and:
///  - tracks any number of named activation chords (up to 3 VK codes each, mouse buttons
///    included) and fires <see cref="ChordPressed"/>/<see cref="ChordReleased"/> with the
///    chord id when one is held / released;
///  - fires <see cref="ZoomScrolled"/> on Shift+wheel while the magnifier is active;
///  - supports a capture mode for rebinding a chord from the settings UI.
/// Hooks are installed on the thread that calls <see cref="Install"/> (the WPF UI thread),
/// so all events are raised on that thread and may touch the UI directly.
/// </summary>
public sealed class HotkeyManager : IDisposable
{
    private sealed class Chord
    {
        public required string Id;
        public required List<int> Keys;
        public bool Matched;
    }

    private readonly LowLevelProc _keyboardProc;
    private readonly LowLevelProc _mouseProc;
    private IntPtr _keyboardHook;
    private IntPtr _mouseHook;

    private readonly HashSet<int> _pressed = new();
    private List<Chord> _chords = new();

    // Capture mode.
    private bool _capturing;
    private readonly HashSet<int> _captureSet = new();
    private Action<IReadOnlyList<int>?>? _captureDone;

    /// <summary>When true, Shift+wheel is captured for zoom (and swallowed).</summary>
    public bool MagnifierActive { get; set; }

    /// <summary>Raised with the chord id when a chord becomes fully held.</summary>
    public event Action<string>? ChordPressed;

    /// <summary>Raised with the chord id when a fully-held chord is broken.</summary>
    public event Action<string>? ChordReleased;

    /// <summary>+1 = zoom in (wheel up), -1 = zoom out (wheel down).</summary>
    public event Action<int>? ZoomScrolled;

    public HotkeyManager()
    {
        _keyboardProc = KeyboardProc;
        _mouseProc = MouseProc;
    }

    public void Install()
    {
        var hMod = GetModuleHandle(null);
        _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, hMod, 0);
        _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, hMod, 0);
        Diagnostics.Log($"Hooks installed kbd={_keyboardHook} mouse={_mouseHook}");
    }

    /// <summary>Replaces the set of active chords. Only registered chords fire events.</summary>
    public void SetChords(IEnumerable<(string id, IReadOnlyList<int> keys)> chords)
    {
        _chords = chords
            .Where(c => c.keys.Count > 0)
            .Select(c => new Chord { Id = c.id, Keys = c.keys.Take(3).Select(Normalize).Distinct().ToList() })
            .Where(c => !IsLoneLeftClick(c.Keys)) // never let a bare left-click be a hotkey
            .ToList();
    }

    /// <summary>
    /// Begins capturing a new chord. The next combination held and released is reported
    /// (max 3 keys); pressing Escape cancels (reports null). Input is swallowed meanwhile.
    /// </summary>
    public void BeginCapture(Action<IReadOnlyList<int>?> onCaptured)
    {
        _capturing = true;
        _captureSet.Clear();
        _captureDone = onCaptured;
    }

    private IntPtr KeyboardProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = (int)wParam;
            var data = System.Runtime.InteropServices.Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            int vk = Normalize((int)data.vkCode);
            bool down = msg is WM_KEYDOWN or WM_SYSKEYDOWN;
            bool up = msg is WM_KEYUP or WM_SYSKEYUP;

            if (HandleInput(vk, down, up, isMouse: false))
                return 1;
        }
        return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private IntPtr MouseProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = (int)wParam;
            var data = System.Runtime.InteropServices.Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);

            if (msg == WM_MOUSEWHEEL)
            {
                if (!_capturing && MagnifierActive && _pressed.Contains(VK_SHIFT))
                {
                    short delta = (short)((data.mouseData >> 16) & 0xFFFF);
                    ZoomScrolled?.Invoke(delta > 0 ? 1 : -1);
                    return 1; // swallow so the page doesn't scroll
                }
                return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
            }

            if (TryMouseButton(msg, data, out int vk, out bool down, out bool up))
            {
                if (HandleInput(vk, down, up, isMouse: true))
                    return 1;
            }
        }
        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private static bool TryMouseButton(int msg, MSLLHOOKSTRUCT data, out int vk, out bool down, out bool up)
    {
        down = false; up = false; vk = 0;
        switch (msg)
        {
            case WM_LBUTTONDOWN: vk = VK_LBUTTON; down = true; return true;
            case WM_LBUTTONUP: vk = VK_LBUTTON; up = true; return true;
            case WM_RBUTTONDOWN: vk = VK_RBUTTON; down = true; return true;
            case WM_RBUTTONUP: vk = VK_RBUTTON; up = true; return true;
            case WM_MBUTTONDOWN: vk = VK_MBUTTON; down = true; return true;
            case WM_MBUTTONUP: vk = VK_MBUTTON; up = true; return true;
            case WM_XBUTTONDOWN: vk = ((data.mouseData >> 16) & 0xFFFF) == 1 ? VK_XBUTTON1 : VK_XBUTTON2; down = true; return true;
            case WM_XBUTTONUP: vk = ((data.mouseData >> 16) & 0xFFFF) == 1 ? VK_XBUTTON1 : VK_XBUTTON2; up = true; return true;
            default: return false;
        }
    }

    /// <summary>Returns true if the input should be swallowed.</summary>
    private bool HandleInput(int vk, bool down, bool up, bool isMouse)
    {
        if (_capturing)
            return HandleCapture(vk, down, up);

        if (down) _pressed.Add(vk);
        else if (up) _pressed.Remove(vk);

        bool suppress = false;
        foreach (var chord in _chords)
        {
            bool matched = chord.Keys.All(_pressed.Contains);

            if (matched && !chord.Matched)
            {
                chord.Matched = true;
                Diagnostics.Log($"chord matched id={chord.Id}");
                ChordPressed?.Invoke(chord.Id);
                // Swallow the completing trigger (e.g. middle-click) but never a plain modifier.
                if (down && (isMouse || !IsModifier(vk)))
                    suppress = true;
            }
            else if (!matched && chord.Matched)
            {
                chord.Matched = false;
                ChordReleased?.Invoke(chord.Id);
            }
        }

        return suppress;
    }

    private bool HandleCapture(int vk, bool down, bool up)
    {
        if (down)
        {
            if (vk == VK_ESCAPE)
            {
                FinishCapture(null);
                return true;
            }
            if (_captureSet.Count < 3)
                _captureSet.Add(vk);
        }
        else if (up && _captureSet.Count > 0)
        {
            // A bare left-click would hijack normal clicking — reject it (treated as cancel).
            FinishCapture(IsLoneLeftClick(_captureSet) ? null : _captureSet.ToList());
        }
        return true; // swallow everything while capturing
    }

    private void FinishCapture(IReadOnlyList<int>? result)
    {
        _capturing = false;
        var done = _captureDone;
        _captureDone = null;
        _captureSet.Clear();
        _pressed.Clear();
        foreach (var c in _chords) c.Matched = false;
        done?.Invoke(result);
    }

    /// <summary>Collapses left/right modifier variants to their generic VK.</summary>
    private static int Normalize(int vk) => vk switch
    {
        VK_LSHIFT or VK_RSHIFT => VK_SHIFT,
        VK_LCONTROL or VK_RCONTROL => VK_CONTROL,
        VK_LMENU or VK_RMENU => VK_MENU,
        _ => vk,
    };

    private static bool IsModifier(int vk) => vk is VK_SHIFT or VK_CONTROL or VK_MENU;

    private static bool IsLoneLeftClick(ICollection<int> keys) =>
        keys.Count == 1 && keys.Contains(VK_LBUTTON);

    public void Dispose()
    {
        if (_keyboardHook != IntPtr.Zero) { UnhookWindowsHookEx(_keyboardHook); _keyboardHook = IntPtr.Zero; }
        if (_mouseHook != IntPtr.Zero) { UnhookWindowsHookEx(_mouseHook); _mouseHook = IntPtr.Zero; }
    }
}
