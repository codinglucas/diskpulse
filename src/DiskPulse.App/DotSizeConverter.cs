using System.Globalization;
using System.Windows.Data;

namespace DiskPulse.App;

/// <summary>Health dot diameter for a font size: a little smaller than the text, like the original 10 px at 12 pt.</summary>
public sealed class DotSizeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is double fontSize ? Math.Round(fontSize * 0.8) : 10d;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
