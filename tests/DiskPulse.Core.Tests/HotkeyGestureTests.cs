using DiskPulse.Core.Settings;

namespace DiskPulse.Core.Tests;

public class HotkeyGestureTests
{
    [Theory]
    [InlineData("Ctrl+Shift+D", HotkeyModifiers.Control | HotkeyModifiers.Shift, "D", "Ctrl+Shift+D")]
    [InlineData("shift + ctrl + d", HotkeyModifiers.Control | HotkeyModifiers.Shift, "D", "Ctrl+Shift+D")]
    [InlineData("Control+Alt+5", HotkeyModifiers.Control | HotkeyModifiers.Alt, "5", "Ctrl+Alt+5")]
    [InlineData("Win+Alt+pageup", HotkeyModifiers.Windows | HotkeyModifiers.Alt, "PageUp", "Alt+Win+PageUp")]
    [InlineData("F9", HotkeyModifiers.None, "F9", "F9")]
    [InlineData("ctrl+f12", HotkeyModifiers.Control, "F12", "Ctrl+F12")]
    [InlineData("Alt+F", HotkeyModifiers.Alt, "F", "Alt+F")]
    public void TryParse_AcceptsValidGestures(string text, HotkeyModifiers modifiers, string key, string canonical)
    {
        Assert.True(HotkeyGesture.TryParse(text, out var gesture));
        Assert.Equal(modifiers, gesture.Modifiers);
        Assert.Equal(key, gesture.Key);
        Assert.Equal(canonical, gesture.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("D")]            // A letter needs a modifier.
    [InlineData("Ctrl+Shift")]   // No key.
    [InlineData("Ctrl+A+B")]     // Two keys.
    [InlineData("Ctrl++D")]      // Empty token.
    [InlineData("Ctrl+F25")]
    [InlineData("Ctrl+Escape")]  // Not a supported key.
    [InlineData("Ctrl+Ç")]
    public void TryParse_RejectsInvalidGestures(string? text)
    {
        Assert.False(HotkeyGesture.TryParse(text, out var gesture));
        Assert.Equal(HotkeyGesture.Default, gesture);
    }

    [Fact]
    public void Default_IsCtrlShiftD() =>
        Assert.Equal("Ctrl+Shift+D", HotkeyGesture.Default.ToString());

    [Fact]
    public void ModifierValues_MatchWin32() =>
        Assert.Equal(
            [0x1, 0x2, 0x4, 0x8],
            new[] { HotkeyModifiers.Alt, HotkeyModifiers.Control, HotkeyModifiers.Shift, HotkeyModifiers.Windows }.Select(m => (int)m));
}
