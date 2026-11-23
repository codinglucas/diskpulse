namespace DiskPulse.Core.Settings;

/// <summary>Modifier keys. Values match the Win32 MOD_* flags used by RegisterHotKey.</summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 0x1,
    Control = 0x2,
    Shift = 0x4,
    Windows = 0x8,
}

/// <summary>
/// A global shortcut such as <c>Ctrl+Shift+D</c>. <see cref="Key"/> is a letter, a digit, an F key,
/// or one of <see cref="NamedKeys"/>. At least one modifier is required, except for F keys.
/// </summary>
public sealed record HotkeyGesture(HotkeyModifiers Modifiers, string Key)
{
    public static HotkeyGesture Default { get; } = new(HotkeyModifiers.Control | HotkeyModifiers.Shift, "D");

    public static IReadOnlyList<string> NamedKeys { get; } =
        ["Space", "Insert", "Delete", "Home", "End", "PageUp", "PageDown", "Up", "Down", "Left", "Right"];

    public static bool TryParse(string? text, out HotkeyGesture gesture)
    {
        gesture = Default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var modifiers = HotkeyModifiers.None;
        string? key = null;
        foreach (string raw in text.Split('+'))
        {
            string token = raw.Trim();
            var modifier = ParseModifier(token);
            if (modifier != HotkeyModifiers.None)
            {
                modifiers |= modifier;
                continue;
            }

            if (key is not null || NormalizeKey(token) is not { } normalized)
                return false; // Two keys, an empty token or an unsupported key.
            key = normalized;
        }

        if (key is null || (modifiers == HotkeyModifiers.None && !IsFunctionKey(key)))
            return false;

        gesture = new HotkeyGesture(modifiers, key);
        return true;
    }

    /// <summary>Canonical text, for example <c>Ctrl+Shift+D</c>.</summary>
    public override string ToString()
    {
        var parts = new List<string>(5);
        if (Modifiers.HasFlag(HotkeyModifiers.Control))
            parts.Add("Ctrl");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt))
            parts.Add("Alt");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift))
            parts.Add("Shift");
        if (Modifiers.HasFlag(HotkeyModifiers.Windows))
            parts.Add("Win");
        parts.Add(Key);
        return string.Join('+', parts);
    }

    private static HotkeyModifiers ParseModifier(string token) => token.ToUpperInvariant() switch
    {
        "CTRL" or "CONTROL" => HotkeyModifiers.Control,
        "ALT" => HotkeyModifiers.Alt,
        "SHIFT" => HotkeyModifiers.Shift,
        "WIN" or "WINDOWS" => HotkeyModifiers.Windows,
        _ => HotkeyModifiers.None,
    };

    private static string? NormalizeKey(string token)
    {
        if (token.Length == 1 && (char.IsAsciiLetter(token[0]) || char.IsAsciiDigit(token[0])))
            return token.ToUpperInvariant();

        if (token.Length is 2 or 3 && token[0] is 'F' or 'f' &&
            int.TryParse(token.AsSpan(1), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int n) &&
            n is >= 1 and <= 24)
            return $"F{n}";

        return NamedKeys.FirstOrDefault(k => string.Equals(k, token, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsFunctionKey(string key) => key.Length > 1 && key[0] == 'F' && char.IsAsciiDigit(key[1]);
}
