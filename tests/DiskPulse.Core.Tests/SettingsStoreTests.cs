using DiskPulse.Core.Settings;

namespace DiskPulse.Core.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "DiskPulseTests", Guid.NewGuid().ToString("N"));

    private string SettingsPath => Path.Combine(_directory, "DiskPulse", "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void MissingFile_GivesDefaults_AndWritesNothing()
    {
        var settings = new SettingsStore(SettingsPath).Load();

        Assert.Equal("Ctrl+Shift+D", settings.Hotkey);
        Assert.False(File.Exists(SettingsPath));
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var store = new SettingsStore(SettingsPath);
        var settings = new AppSettings { RefreshIntervalSeconds = 2, Language = "pt-BR", Hotkey = "Alt+F9" };
        settings.Overlay.ClickThrough = true;
        settings.Overlay.ShowQueueLength = false;
        settings.Overlay.LastMonitor = @"\\.\DISPLAY2";
        settings.Overlay.Positions[@"\\.\DISPLAY2"] = new OverlayPosition { X = -40, Y = 300 };
        settings.Thresholds.SlowResponseMs = 75;

        store.Save(settings);
        var loaded = store.Load();

        Assert.Equal(2, loaded.RefreshIntervalSeconds);
        Assert.Equal("pt-BR", loaded.Language);
        Assert.Equal("Alt+F9", loaded.Hotkey);
        Assert.True(loaded.Overlay.ClickThrough);
        Assert.False(loaded.Overlay.ShowQueueLength);
        Assert.Equal(@"\\.\DISPLAY2", loaded.Overlay.LastMonitor);
        Assert.Equal(-40, loaded.Overlay.Positions[@"\\.\DISPLAY2"].X);
        Assert.Equal(75, loaded.Thresholds.SlowResponseMs);
        Assert.False(File.Exists(SettingsPath + ".tmp"));
    }

    [Fact]
    public void SavedFile_UsesReadableCamelCaseJson()
    {
        new SettingsStore(SettingsPath).Save(new AppSettings());

        string json = File.ReadAllText(SettingsPath);
        Assert.Contains("\"refreshIntervalSeconds\": 1", json);
        Assert.Contains("\"slowResponseMs\": 50", json);
        Assert.DoesNotContain("refreshInterval\"", json); // The computed TimeSpan is not stored.
    }

    [Fact]
    public void DamagedFile_GivesDefaults_AndKeepsACopy()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, "{ \"hotkey\": \"Alt+X\", oops");
        var store = new SettingsStore(SettingsPath);

        var settings = store.Load();

        Assert.Equal("Ctrl+Shift+D", settings.Hotkey);
        Assert.Equal("{ \"hotkey\": \"Alt+X\", oops", File.ReadAllText(store.InvalidCopyPath));
    }

    [Fact]
    public void HandEditedFile_WithCommentsAndOutOfRangeValues_IsNormalized()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, """
            {
              // edited by hand
              "refreshIntervalSeconds": 0.01,
              "language": "xx",
              "overlay": { "opacity": 0, "unknownSetting": true },
              "thresholds": null,
            }
            """);

        var settings = new SettingsStore(SettingsPath).Load();

        Assert.Equal(AppSettings.MinRefreshSeconds, settings.RefreshIntervalSeconds);
        Assert.Equal("", settings.Language);
        Assert.Equal(OverlaySettings.MinOpacity, settings.Overlay.Opacity);
        Assert.Equal(DiskThresholds.Default, settings.Thresholds.ToDiskThresholds());
    }

    [Fact]
    public void OversizedFile_IsNotParsed()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, "{\"hotkey\": \"Alt+X\"," + new string(' ', (int)SettingsStore.MaxFileBytes) + "}");

        var settings = new SettingsStore(SettingsPath).Load();

        Assert.Equal("Ctrl+Shift+D", settings.Hotkey);
    }
}
