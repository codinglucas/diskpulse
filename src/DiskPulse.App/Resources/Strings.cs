using System.Globalization;
using System.Resources;

namespace DiskPulse.App.Resources;

/// <summary>
/// UI strings from Strings.resx (English) and its translations (Strings.pt-BR.resx), picked by
/// the current UI culture. Keys are listed in Strings.resx.
/// </summary>
internal static class Strings
{
    private static readonly ResourceManager Manager = new(typeof(Strings).FullName!, typeof(Strings).Assembly);

    public static string Get(string key) => Manager.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    /// <summary>Formats a string with the current culture, so numbers read "1,5" on pt-BR.</summary>
    public static string Format(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), args);
}
