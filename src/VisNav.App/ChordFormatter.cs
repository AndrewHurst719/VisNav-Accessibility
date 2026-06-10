using System.Text;
using static VisNav.App.Interop.HookNative;

namespace VisNav.App;

/// <summary>Turns a list of virtual-key codes into a human-readable hotkey string.</summary>
public static class ChordFormatter
{
    public static string Describe(IReadOnlyList<int>? chord)
    {
        if (chord is null || chord.Count == 0)
            return "(none)";

        // Show modifiers first for natural reading (Ctrl + Shift + …).
        var ordered = chord
            .OrderBy(vk => IsModifier(vk) ? 0 : 1)
            .ThenBy(vk => vk)
            .ToList();

        return string.Join(" + ", ordered.Select(Name));
    }

    private static bool IsModifier(int vk) => vk is VK_SHIFT or VK_CONTROL or VK_MENU;

    public static string Name(int vk) => vk switch
    {
        VK_LBUTTON => "Left-click",
        VK_RBUTTON => "Right-click",
        VK_MBUTTON => "Middle-click",
        VK_XBUTTON1 => "Mouse 4",
        VK_XBUTTON2 => "Mouse 5",
        VK_CONTROL => "Ctrl",
        VK_SHIFT => "Shift",
        VK_MENU => "Alt",
        VK_ESCAPE => "Esc",
        VK_BACK => "Backspace",
        0x20 => "Space",
        0x0D => "Enter",
        0x09 => "Tab",
        >= 0x70 and <= 0x7B => "F" + (vk - 0x6F),          // F1–F12
        >= 0x30 and <= 0x39 => ((char)vk).ToString(),       // 0–9
        >= 0x41 and <= 0x5A => ((char)vk).ToString(),       // A–Z
        _ => "Key " + vk,
    };
}
