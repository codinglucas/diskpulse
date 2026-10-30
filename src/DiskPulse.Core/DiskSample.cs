namespace DiskPulse.Core;

/// <summary>One refresh worth of metrics for a single physical disk, in display units.</summary>
public sealed record DiskSample(
    int DiskNumber,
    string InstanceName,
    DateTimeOffset Timestamp,
    double ResponseMs,
    double ReadResponseMs,
    double WriteResponseMs,
    double ActivePercent,
    double QueueLength,
    double ReadMBps,
    double WriteMBps)
{
    public double TotalMBps => ReadMBps + WriteMBps;

    public static DiskSample FromRaw(int diskNumber, string instanceName, DateTimeOffset timestamp, in RawDiskCounters raw) =>
        new(
            diskNumber,
            instanceName,
            timestamp,
            ResponseMs: Units.SecondsToMilliseconds(raw.SecondsPerTransfer),
            ReadResponseMs: Units.SecondsToMilliseconds(raw.SecondsPerRead),
            WriteResponseMs: Units.SecondsToMilliseconds(raw.SecondsPerWrite),
            ActivePercent: Units.ActivePercentFromIdle(raw.IdlePercent),
            QueueLength: Units.Sanitize(raw.QueueLength),
            ReadMBps: Units.BytesToMegabytes(raw.ReadBytesPerSecond),
            WriteMBps: Units.BytesToMegabytes(raw.WriteBytesPerSecond));
}
