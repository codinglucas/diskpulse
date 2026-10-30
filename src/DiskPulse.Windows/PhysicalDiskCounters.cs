namespace DiskPulse.Windows;

/// <summary>
/// English counter paths. They are passed to PdhAddEnglishCounterW, which resolves them on
/// any Windows display language. Never replace these with localized names.
/// </summary>
public static class PhysicalDiskCounters
{
    public const string SecondsPerTransfer = @"\PhysicalDisk(*)\Avg. Disk sec/Transfer";
    public const string SecondsPerRead = @"\PhysicalDisk(*)\Avg. Disk sec/Read";
    public const string SecondsPerWrite = @"\PhysicalDisk(*)\Avg. Disk sec/Write";
    public const string IdleTime = @"\PhysicalDisk(*)\% Idle Time";
    public const string QueueLength = @"\PhysicalDisk(*)\Current Disk Queue Length";
    public const string ReadBytesPerSecond = @"\PhysicalDisk(*)\Disk Read Bytes/sec";
    public const string WriteBytesPerSecond = @"\PhysicalDisk(*)\Disk Write Bytes/sec";

    public static readonly IReadOnlyList<string> All =
    [
        SecondsPerTransfer,
        SecondsPerRead,
        SecondsPerWrite,
        IdleTime,
        QueueLength,
        ReadBytesPerSecond,
        WriteBytesPerSecond,
    ];
}
