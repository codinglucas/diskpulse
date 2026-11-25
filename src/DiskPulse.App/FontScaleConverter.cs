using System.Globalization;
using System.Windows.Data;

namespace DiskPulse.App;

/// <summary>
/// Sizes something from the overlay's font size, so it grows with the font-size setting. The
/// parameter is the factor, e.g. 0.8 for the health dot (10 px at the default 12 pt).
/// </summary>
public sealed class FontScaleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double factor = parameter is string text && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double f) ? f : 1;
        return value is double fontSize ? Math.Round(fontSize * factor) : 10d * factor;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
