namespace DiskPulse.Core;

/// <summary>Counter values for one disk exactly as the OS reports them, before any conversion.</summary>
/// <param name="SecondsPerTransfer">Avg. Disk sec/Transfer.</param>
/// <param name="SecondsPerRead">Avg. Disk sec/Read.</param>
/// <param name="SecondsPerWrite">Avg. Disk sec/Write.</param>
/// <param name="IdlePercent">% Idle Time.</param>
/// <param name="QueueLength">Current Disk Queue Length.</param>
/// <param name="ReadBytesPerSecond">Disk Read Bytes/sec.</param>
/// <param name="WriteBytesPerSecond">Disk Write Bytes/sec.</param>
public readonly record struct RawDiskCounters(
    double SecondsPerTransfer,
    double SecondsPerRead,
    double SecondsPerWrite,
    double IdlePercent,
    double QueueLength,
    double ReadBytesPerSecond,
    double WriteBytesPerSecond);
