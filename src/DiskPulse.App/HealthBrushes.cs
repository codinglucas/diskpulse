using System.Windows.Media;
using DiskPulse.Core;

namespace DiskPulse.App;

internal static class HealthBrushes
{
    public static readonly Brush Ok = Create(0x4C, 0xAF, 0x50);
    public static readonly Brush Warning = Create(0xFF, 0xC1, 0x07);
    public static readonly Brush Critical = Create(0xF4, 0x43, 0x36);

    /// <summary>Secondary text such as status messages; not a health color.</summary>
    public static readonly Brush Muted = Create(0xBB, 0xBB, 0xBB);

    public static Brush For(DiskHealth health) => health switch
    {
        DiskHealth.Critical => Critical,
        DiskHealth.Warning => Warning,
        _ => Ok,
    };

    private static Brush Create(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze(); // Frozen brushes can be shared and are cheaper to render.
        return brush;
    }
}
