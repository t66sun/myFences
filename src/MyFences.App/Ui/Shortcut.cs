using System.Windows.Input;

namespace MyFences.App.Ui;

internal static class Shortcut
{
    public static (uint Modifiers, uint Key) Parse(string text)
    {
        uint modifiers = 0; string? key = null;
        foreach (var part in text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            switch (part.ToUpperInvariant())
            {
                case "CTRL": case "CONTROL": modifiers |= 2; break;
                case "ALT": modifiers |= 1; break;
                case "SHIFT": modifiers |= 4; break;
                default: if (key is not null) throw new ArgumentException(Text.Get("hotkeyHint")); key = part.ToUpperInvariant(); break;
            }
        }
        if (modifiers == 0 || key is null) throw new ArgumentException(Text.Get("hotkeyHint"));
        if (key.Length == 1 && char.IsDigit(key[0])) key = "D" + key;
        if (!Enum.TryParse<Key>(key, true, out var parsed) || !((parsed >= System.Windows.Input.Key.A && parsed <= System.Windows.Input.Key.Z) || (parsed >= System.Windows.Input.Key.D0 && parsed <= System.Windows.Input.Key.D9) || (parsed >= System.Windows.Input.Key.F1 && parsed <= System.Windows.Input.Key.F24)))
            throw new ArgumentException(Text.Get("hotkeyHint"));
        return (modifiers, (uint)KeyInterop.VirtualKeyFromKey(parsed));
    }
}
