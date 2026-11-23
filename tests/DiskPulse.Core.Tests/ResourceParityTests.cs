using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace DiskPulse.Core.Tests;

/// <summary>
/// Checks the overlay's translations against the English strings. A missing key shows the raw key
/// to users; a mismatched placeholder makes string.Format throw at runtime. Reads the .resx files
/// from the source tree, so it runs on any OS without loading the WPF app.
/// </summary>
public partial class ResourceParityTests
{
    private static readonly string ResourcesDirectory = Path.Combine(FindRepositoryRoot(), "src", "DiskPulse.App", "Resources");

    public static TheoryData<string> Translations()
    {
        var data = new TheoryData<string>();
        foreach (string file in Directory.GetFiles(ResourcesDirectory, "Strings.*.resx"))
            data.Add(Path.GetFileName(file));
        return data;
    }

    [Fact]
    public void PortugueseTranslation_Exists() =>
        Assert.True(File.Exists(Path.Combine(ResourcesDirectory, "Strings.pt-BR.resx")));

    [Theory]
    [MemberData(nameof(Translations))]
    public void Translation_HasExactlyTheEnglishKeys(string file)
    {
        var english = Load("Strings.resx");
        var translated = Load(file);

        Assert.Empty(english.Keys.Except(translated.Keys)); // Missing translations.
        Assert.Empty(translated.Keys.Except(english.Keys)); // Leftovers from removed strings.
    }

    [Theory]
    [MemberData(nameof(Translations))]
    public void Translation_UsesTheSamePlaceholders(string file)
    {
        var english = Load("Strings.resx");
        var translated = Load(file);

        foreach (var (key, value) in english)
        {
            if (translated.TryGetValue(key, out string? other))
                Assert.True(Placeholders(value).SetEquals(Placeholders(other)), $"{file}: placeholders differ for {key}");
        }
    }

    [Fact]
    public void EveryString_IsAValidFormatString()
    {
        foreach (string file in Directory.GetFiles(ResourcesDirectory, "Strings*.resx"))
        {
            foreach (var (key, value) in Load(Path.GetFileName(file)))
            {
                var args = Enumerable.Repeat<object>(1.5, 10).ToArray();
                var exception = Record.Exception(() => string.Format(value, args));
                Assert.True(exception is null, $"{Path.GetFileName(file)}: {key} is not a valid format string");
            }
        }
    }

    private static Dictionary<string, string> Load(string file) =>
        XDocument.Load(Path.Combine(ResourcesDirectory, file)).Root!
            .Elements("data")
            .ToDictionary(e => (string)e.Attribute("name")!, e => (string?)e.Element("value") ?? "");

    private static HashSet<int> Placeholders(string value) =>
        PlaceholderPattern().Matches(value).Select(m => int.Parse(m.Groups[1].Value)).ToHashSet();

    [GeneratedRegex(@"\{(\d+)(?:[,:][^}]*)?\}")]
    private static partial Regex PlaceholderPattern();

    private static string FindRepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "DiskPulse.sln")))
                return dir.FullName;
        }
        throw new DirectoryNotFoundException("Could not find DiskPulse.sln above the test output folder.");
    }
}
