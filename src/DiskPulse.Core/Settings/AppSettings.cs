using System.Text.Json;
using System.Text.Json.Serialization;
using DiskPulse.Core.Logging;

namespace DiskPulse.Core.Settings;

/// <summary>
/// Everything the user can change, stored as JSON. The file may be hand-edited or damaged, so
/// <see cref="Normalize"/> must run after loading: it replaces missing or out-of-range values.
/// </summary>
public sealed class AppSettings
{
    public const double MinRefreshSeconds = 0.5;
    public const double MaxRefreshSeconds = 5;

    /// <summary>"" follows the Windows display language.</summary>
    public static IReadOnlyList<string> SupportedLanguages { get; } = ["", "en", "pt-BR"];

    public double RefreshIntervalSeconds { get; set; } = 1;

    public string Language { get; set; } = "";

    public string Hotkey { get; set; } = HotkeyGesture.Default.ToString();

    public OverlaySettings Overlay { get; set; } = new();

    public ThresholdSettings Thresholds { get; set; } = new();

    public LoggingSettings Logging { get; set; } = new();

    [JsonIgnore]
    public TimeSpan RefreshInterval => TimeSpan.FromSeconds(RefreshIntervalSeconds);

    public AppSettings Clone() =>
        JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(this, SettingsJson.Options), SettingsJson.Options)!;

    public void Normalize()
    {
        RefreshIntervalSeconds = Limits.Clamp(RefreshIntervalSeconds, MinRefreshSeconds, MaxRefreshSeconds, 1);

        if (!SupportedLanguages.Contains(Language ?? ""))
            Language = "";
        Language ??= "";

        Hotkey = HotkeyGesture.TryParse(Hotkey, out var gesture) ? gesture.ToString() : HotkeyGesture.Default.ToString();

        Overlay ??= new OverlaySettings();
        Overlay.Normalize();
        Thresholds ??= new ThresholdSettings();
        Thresholds.Normalize();
        Logging ??= new LoggingSettings();
        Logging.Normalize();
    }
}

public sealed class OverlaySettings
{
    public const double MinOpacity = 0.2;
    public const double MaxOpacity = 1;
    public const double MinFontSize = 8;
    public const double MaxFontSize = 24;
    private const int MaxRememberedMonitors = 16;

    public bool Visible { get; set; } = true;

    /// <summary>Mouse clicks pass through the overlay to the window underneath.</summary>
    public bool ClickThrough { get; set; }

    public double Opacity { get; set; } = 0.85;

    public double FontSize { get; set; } = 12;

    public bool ShowResponseTime { get; set; } = true;
    public bool ShowActiveTime { get; set; } = true;
    public bool ShowQueueLength { get; set; } = true;
    public bool ShowThroughput { get; set; } = true;

    /// <summary>Shows a 60-second response-time graph under each disk.</summary>
    public bool Expanded { get; set; }

    /// <summary>Device name of the monitor the overlay was last moved to, e.g. <c>\\.\DISPLAY2</c>.</summary>
    public string? LastMonitor { get; set; }

    /// <summary>Overlay position per monitor, in pixels from the top-left of that monitor's work area.</summary>
    public Dictionary<string, OverlayPosition> Positions { get; set; } = new();

    public void Normalize()
    {
        Opacity = Limits.Clamp(Opacity, MinOpacity, MaxOpacity, 0.85);
        FontSize = Limits.Clamp(FontSize, MinFontSize, MaxFontSize, 12);

        Positions = (Positions ?? new())
            .Where(p => !string.IsNullOrWhiteSpace(p.Key) && p.Value is not null)
            .Take(MaxRememberedMonitors)
            .ToDictionary(p => p.Key, p => p.Value);
        if (LastMonitor is not null && !Positions.ContainsKey(LastMonitor))
            LastMonitor = null;
    }
}

public sealed class OverlayPosition
{
    public int X { get; set; }
    public int Y { get; set; }
}

/// <summary>User-editable form of <see cref="DiskThresholds"/>, with durations in seconds.</summary>
public sealed class ThresholdSettings
{
    private static readonly DiskThresholds Defaults = DiskThresholds.Default;

    public double SlowResponseMs { get; set; } = Defaults.SlowResponseMs;
    public double SlowResponseSeconds { get; set; } = Defaults.SlowResponseDuration.TotalSeconds;

    public double VerySlowResponseMs { get; set; } = Defaults.VerySlowResponseMs;
    public double VerySlowResponseSeconds { get; set; } = Defaults.VerySlowResponseDuration.TotalSeconds;

    public double HighQueueLength { get; set; } = Defaults.HighQueueLength;
    public double HighQueueMinResponseMs { get; set; } = Defaults.HighQueueMinResponseMs;
    public double HighQueueSeconds { get; set; } = Defaults.HighQueueDuration.TotalSeconds;

    public double BusyActivePercent { get; set; } = Defaults.BusyActivePercent;
    public double BusyMaxThroughputMBps { get; set; } = Defaults.BusyMaxThroughputMBps;
    public double BusySeconds { get; set; } = Defaults.BusyDuration.TotalSeconds;

    public DiskThresholds ToDiskThresholds() => new()
    {
        SlowResponseMs = SlowResponseMs,
        SlowResponseDuration = TimeSpan.FromSeconds(SlowResponseSeconds),
        VerySlowResponseMs = VerySlowResponseMs,
        VerySlowResponseDuration = TimeSpan.FromSeconds(VerySlowResponseSeconds),
        HighQueueLength = HighQueueLength,
        HighQueueMinResponseMs = HighQueueMinResponseMs,
        HighQueueDuration = TimeSpan.FromSeconds(HighQueueSeconds),
        BusyActivePercent = BusyActivePercent,
        BusyMaxThroughputMBps = BusyMaxThroughputMBps,
        BusyDuration = TimeSpan.FromSeconds(BusySeconds),
    };

    public void Normalize()
    {
        const double MaxMs = 60_000;
        const double MinSeconds = 1;
        const double MaxSeconds = 600;

        SlowResponseMs = Limits.Clamp(SlowResponseMs, 1, MaxMs, Defaults.SlowResponseMs);
        SlowResponseSeconds = Limits.Clamp(SlowResponseSeconds, MinSeconds, MaxSeconds, Defaults.SlowResponseDuration.TotalSeconds);
        VerySlowResponseMs = Limits.Clamp(VerySlowResponseMs, 1, MaxMs, Defaults.VerySlowResponseMs);
        VerySlowResponseSeconds = Limits.Clamp(VerySlowResponseSeconds, MinSeconds, MaxSeconds, Defaults.VerySlowResponseDuration.TotalSeconds);
        HighQueueLength = Limits.Clamp(HighQueueLength, 0, 10_000, Defaults.HighQueueLength);
        HighQueueMinResponseMs = Limits.Clamp(HighQueueMinResponseMs, 0, MaxMs, Defaults.HighQueueMinResponseMs);
        HighQueueSeconds = Limits.Clamp(HighQueueSeconds, MinSeconds, MaxSeconds, Defaults.HighQueueDuration.TotalSeconds);
        BusyActivePercent = Limits.Clamp(BusyActivePercent, 1, 100, Defaults.BusyActivePercent);
        BusyMaxThroughputMBps = Limits.Clamp(BusyMaxThroughputMBps, 0.01, 100_000, Defaults.BusyMaxThroughputMBps);
        BusySeconds = Limits.Clamp(BusySeconds, MinSeconds, MaxSeconds, Defaults.BusyDuration.TotalSeconds);
    }
}

/// <summary>Optional CSV log, one file per disk. Off by default: DiskPulse writes nothing unless asked.</summary>
public sealed class LoggingSettings
{
    public const double MinFileSizeMB = 1;
    public const double MaxFileSizeMBLimit = 1000;
    public const int MaxFilesToKeep = 1000;

    public bool Enabled { get; set; }

    /// <summary>Full path of the log folder. "" means <see cref="DefaultFolder"/>.</summary>
    public string Folder { get; set; } = "";

    /// <summary>A new file is started once the current one reaches this size.</summary>
    public double MaxFileSizeMB { get; set; } = 10;

    /// <summary>Files kept per disk, the current one included. Older ones are deleted.</summary>
    public int FilesToKeep { get; set; } = 10;

    public static string DefaultFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DiskPulse", "Logs");

    [JsonIgnore]
    public string EffectiveFolder => Folder.Length == 0 ? DefaultFolder : Folder;

    /// <summary>What the logger needs, or null when logging is off.</summary>
    public CsvLogOptions? ToLogOptions() =>
        Enabled ? new CsvLogOptions(EffectiveFolder, (long)(MaxFileSizeMB * Units.BytesPerMegabyte), FilesToKeep) : null;

    /// <summary>True for a full path such as <c>D:\Logs</c>; a relative path would depend on the working folder.</summary>
    public static bool IsValidFolder(string? folder) =>
        !string.IsNullOrWhiteSpace(folder) &&
        folder.IndexOfAny(Path.GetInvalidPathChars()) < 0 &&
        Path.IsPathFullyQualified(folder);

    public void Normalize()
    {
        Folder = Folder?.Trim() ?? "";
        if (Folder.Length > 0 && !IsValidFolder(Folder))
            Folder = "";
        MaxFileSizeMB = Limits.Clamp(MaxFileSizeMB, MinFileSizeMB, MaxFileSizeMBLimit, 10);
        FilesToKeep = Math.Clamp(FilesToKeep, 1, MaxFilesToKeep);
    }
}

internal static class Limits
{
    /// <summary>Like Math.Clamp, but NaN and infinity become <paramref name="fallback"/>.</summary>
    public static double Clamp(double value, double min, double max, double fallback) =>
        double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
}

public static class SettingsJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        // Friendlier for people who edit the file by hand.
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };
}
