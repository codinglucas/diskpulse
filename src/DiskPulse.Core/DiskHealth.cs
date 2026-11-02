namespace DiskPulse.Core;

/// <summary>Overall color of a disk: green, yellow or red.</summary>
public enum DiskHealth
{
    Ok,
    Warning,
    Critical,
}

/// <summary>A specific problem detected on a disk. Each one has a plain-language explanation in the UI.</summary>
public enum DiskWarning
{
    /// <summary>Response time above <see cref="DiskThresholds.SlowResponseMs"/>, sustained.</summary>
    SlowResponse,

    /// <summary>
    /// Queue length above <see cref="DiskThresholds.HighQueueLength"/> with response time above
    /// <see cref="DiskThresholds.HighQueueMinResponseMs"/>, sustained.
    /// </summary>
    HighQueue,

    /// <summary>Response time above <see cref="DiskThresholds.VerySlowResponseMs"/>, sustained.</summary>
    VerySlowResponse,

    /// <summary>Nearly always active while moving almost no data, sustained. Typical of a failing drive or bad cable.</summary>
    BusyNotMoving,
}

public static class DiskWarningExtensions
{
    public static DiskHealth Severity(this DiskWarning warning) => warning switch
    {
        DiskWarning.SlowResponse or DiskWarning.HighQueue => DiskHealth.Warning,
        _ => DiskHealth.Critical,
    };
}

/// <summary>Result of evaluating one sample. <see cref="Warnings"/> is ordered most severe first.</summary>
public sealed record DiskStatus(int DiskNumber, DiskHealth Health, IReadOnlyList<DiskWarning> Warnings);

/// <summary>Limits for the "struggling disk" rules. Each condition must hold for its whole duration.</summary>
public sealed record DiskThresholds
{
    public static DiskThresholds Default { get; } = new();

    public double SlowResponseMs { get; init; } = 50;
    public TimeSpan SlowResponseDuration { get; init; } = TimeSpan.FromSeconds(5);

    public double HighQueueLength { get; init; } = 2;

    /// <summary>A long queue alone is normal during big copies; it only counts once responses slow down too.</summary>
    public double HighQueueMinResponseMs { get; init; } = 20;
    public TimeSpan HighQueueDuration { get; init; } = TimeSpan.FromSeconds(5);

    public double VerySlowResponseMs { get; init; } = 200;
    public TimeSpan VerySlowResponseDuration { get; init; } = TimeSpan.FromSeconds(5);

    public double BusyActivePercent { get; init; } = 90;
    public double BusyMaxThroughputMBps { get; init; } = 1;
    public TimeSpan BusyDuration { get; init; } = TimeSpan.FromSeconds(10);
}
