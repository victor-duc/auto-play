using System.Windows.Input;

namespace AutoPlay.App.Services;

/// <summary>Converts between shortcut texts ("Ctrl+Shift+F9") and the keys used to register global hotkeys.</summary>
public static class HotkeyGesture
{
    private const uint ModAlt = 0x1;
    private const uint ModControl = 0x2;
    private const uint ModShift = 0x4;
    private const uint ModWin = 0x8;

    /// <summary>
    /// Returns the text of a combination, or null if it is not usable as a global shortcut (modifier alone, or a
    /// plain letter or digit without Ctrl or Alt, which would be stolen from every application).
    /// </summary>
    public static string? Format(Key key, ModifierKeys modifiers)
    {
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt
            or Key.LWin or Key.RWin or Key.None)
        {
            return null;
        }

        var isFunctionKey = key is >= Key.F1 and <= Key.F24;
        if (!isFunctionKey && (modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) == 0)
        {
            return null;
        }

        var parts = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            parts.Add("Ctrl");
        }

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            parts.Add("Alt");
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            parts.Add("Shift");
        }

        if (modifiers.HasFlag(ModifierKeys.Windows))
        {
            parts.Add("Win");
        }

        parts.Add(key.ToString());
        return string.Join("+", parts);
    }

    /// <summary>Parses a shortcut text into a virtual-key code and RegisterHotKey modifiers.</summary>
    public static bool TryParse(string text, out uint virtualKey, out uint modifiers)
    {
        virtualKey = 0;
        modifiers = 0;
        var parts = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        foreach (var part in parts[..^1])
        {
            modifiers |= part.ToUpperInvariant() switch
            {
                "CTRL" or "CONTROL" => ModControl,
                "ALT" => ModAlt,
                "SHIFT" => ModShift,
                "WIN" => ModWin,
                _ => 0u,
            };
        }

        if (!Enum.TryParse<Key>(parts[^1], ignoreCase: true, out var key))
        {
            return false;
        }

        virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        return virtualKey != 0;
    }
}
