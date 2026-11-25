using DiskPulse.Core.Settings;

namespace DiskPulse.Core.Tests;

public class SettingsTests
{
    [Fact]
    public void Defaults_MatchSpec()
    {
        var settings = new AppSettings();

        Assert.Equal(1, settings.RefreshIntervalSeconds);
        Assert.Equal("", settings.Language);
        Assert.Equal("Ctrl+Shift+D", settings.Hotkey);
        Assert.True(settings.Overlay.Visible);
        Assert.False(settings.Overlay.ClickThrough);
        Assert.Equal(DiskThresholds.Default, settings.Thresholds.ToDiskThresholds());
    }

    [Theory]
    [InlineData(0.1, 0.5)]
    [InlineData(60, 5)]
    [InlineData(2, 2)]
    [InlineData(double.NaN, 1)]
    [InlineData(double.PositiveInfinity, 1)]
    public void Normalize_ClampsRefreshInterval(double value, double expected)
    {
        var settings = new AppSettings { RefreshIntervalSeconds = value };

        settings.Normalize();

        Assert.Equal(expected, settings.RefreshIntervalSeconds);
    }

    [Theory]
    [InlineData("pt-BR", "pt-BR")]
    [InlineData("en", "en")]
    [InlineData("", "")]
    [InlineData("fr", "")]
    [InlineData(null, "")]
    public void Normalize_OnlyKeepsSupportedLanguages(string? language, string expected)
    {
        var settings = new AppSettings { Language = language! };

        settings.Normalize();

        Assert.Equal(expected, settings.Language);
    }

    [Theory]
    [InlineData("alt+shift+x", "Alt+Shift+X")]
    [InlineData("D", "Ctrl+Shift+D")]
    [InlineData(null, "Ctrl+Shift+D")]
    public void Normalize_CanonicalizesOrResetsHotkey(string? hotkey, string expected)
    {
        var settings = new AppSettings { Hotkey = hotkey! };

        settings.Normalize();

        Assert.Equal(expected, settings.Hotkey);
    }

    [Fact]
    public void Normalize_ClampsOverlayValues()
    {
        var settings = new AppSettings();
        settings.Overlay.Opacity = 5;
        settings.Overlay.FontSize = double.NaN;

        settings.Normalize();

        Assert.Equal(1, settings.Overlay.Opacity);
        Assert.Equal(12, settings.Overlay.FontSize);
    }

    [Fact]
    public void Normalize_ReplacesMissingSections()
    {
        var settings = new AppSettings { Overlay = null!, Thresholds = null! };

        settings.Normalize();

        Assert.NotNull(settings.Overlay);
        Assert.NotNull(settings.Thresholds);
    }

    [Fact]
    public void Normalize_DropsLastMonitorWithoutAPosition()
    {
        var settings = new AppSettings();
        settings.Overlay.LastMonitor = @"\\.\DISPLAY9";

        settings.Normalize();

        Assert.Null(settings.Overlay.LastMonitor);
    }

    [Fact]
    public void Normalize_ClampsThresholds()
    {
        var settings = new AppSettings();
        settings.Thresholds.SlowResponseMs = -10;
        settings.Thresholds.BusyActivePercent = 250;
        settings.Thresholds.BusySeconds = 0;
        settings.Thresholds.HighQueueLength = double.NaN;

        settings.Normalize();

        Assert.Equal(1, settings.Thresholds.SlowResponseMs);
        Assert.Equal(100, settings.Thresholds.BusyActivePercent);
        Assert.Equal(1, settings.Thresholds.BusySeconds);
        Assert.Equal(DiskThresholds.Default.HighQueueLength, settings.Thresholds.HighQueueLength);
    }

    [Fact]
    public void ToDiskThresholds_ConvertsSecondsToDurations()
    {
        var thresholds = new ThresholdSettings { SlowResponseMs = 30, SlowResponseSeconds = 2.5, BusySeconds = 20 }.ToDiskThresholds();

        Assert.Equal(30, thresholds.SlowResponseMs);
        Assert.Equal(TimeSpan.FromSeconds(2.5), thresholds.SlowResponseDuration);
        Assert.Equal(TimeSpan.FromSeconds(20), thresholds.BusyDuration);
    }

    [Fact]
    public void Clone_IsDeep()
    {
        var original = new AppSettings();
        original.Overlay.Positions[@"\\.\DISPLAY1"] = new OverlayPosition { X = 10, Y = 20 };

        var copy = original.Clone();
        copy.Overlay.Positions[@"\\.\DISPLAY1"].X = 99;
        copy.Thresholds.SlowResponseMs = 1;

        Assert.Equal(10, original.Overlay.Positions[@"\\.\DISPLAY1"].X);
        Assert.Equal(DiskThresholds.Default.SlowResponseMs, original.Thresholds.SlowResponseMs);
    }

    [Fact]
    public void Logging_IsOffByDefault()
    {
        var settings = new AppSettings();

        Assert.False(settings.Logging.Enabled);
        Assert.Null(settings.Logging.ToLogOptions());
        Assert.False(settings.Overlay.Expanded);
    }

    [Fact]
    public void Logging_ToLogOptions_UsesDefaultFolderAndBytes()
    {
        var logging = new LoggingSettings { Enabled = true, MaxFileSizeMB = 2, FilesToKeep = 3 };

        var options = logging.ToLogOptions();

        Assert.Equal(new DiskPulse.Core.Logging.CsvLogOptions(LoggingSettings.DefaultFolder, 2 * 1024 * 1024, 3), options);
    }

    [Theory]
    [InlineData("relative\\logs")]
    [InlineData("logs")]
    [InlineData("   ")]
    public void Logging_Normalize_DropsFoldersThatAreNotFullPaths(string folder)
    {
        var logging = new LoggingSettings { Folder = folder };

        logging.Normalize();

        Assert.Equal("", logging.Folder);
        Assert.Equal(LoggingSettings.DefaultFolder, logging.EffectiveFolder);
    }

    [Fact]
    public void Logging_Normalize_KeepsAFullPath()
    {
        string folder = Path.Combine(Path.GetTempPath(), "DiskPulse logs");
        var logging = new LoggingSettings { Folder = "  " + folder + " " };

        logging.Normalize();

        Assert.Equal(folder, logging.Folder);
    }

    [Fact]
    public void Logging_Normalize_ClampsSizes()
    {
        var settings = new AppSettings { Logging = null! };
        settings.Normalize();
        settings.Logging.MaxFileSizeMB = double.NaN;
        settings.Logging.FilesToKeep = 0;

        settings.Normalize();

        Assert.Equal(10, settings.Logging.MaxFileSizeMB);
        Assert.Equal(1, settings.Logging.FilesToKeep);
    }
}
