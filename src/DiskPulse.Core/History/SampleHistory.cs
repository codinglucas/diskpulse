using DiskPulse.Core.Settings;

namespace DiskPulse.Core.History;

/// <summary>
/// The last few minutes of readings per disk, kept in memory for the sparkline and for
/// "Export last 10 minutes". Not thread-safe: the app only touches it on the UI thread.
/// </summary>
public sealed class SampleHistory
{
    public static readonly TimeSpan DefaultRetention = TimeSpan.FromMinutes(10);

    /// <summary>A reading this far behind the newest one means the system clock was set back.</summary>
    private static readonly TimeSpan ClockJumpTolerance = TimeSpan.FromMinutes(1);

    private readonly Dictionary<int, Queue<DiskReading>> _disks = new();
    private readonly int _maxPerDisk;
    private DateTimeOffset? _newest;

    public SampleHistory(TimeSpan retention)
    {
        if (retention <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(retention), "Retention must be positive.");

        Retention = retention;
        // Enough for the fastest refresh rate, with slack; a hard cap in case timestamps misbehave.
        _maxPerDisk = (int)(retention.TotalSeconds / AppSettings.MinRefreshSeconds) + 100;
    }

    public SampleHistory() : this(DefaultRetention)
    {
    }

    public TimeSpan Retention { get; }

    /// <summary>Timestamp of the newest reading, or null when empty.</summary>
    public DateTimeOffset? Newest => _newest;

    public int Count => _disks.Values.Sum(q => q.Count);

    public void Add(IEnumerable<DiskReading> readings)
    {
        foreach (var reading in readings)
        {
            var time = reading.Sample.Timestamp;
            if (_newest is { } newest && time < newest - ClockJumpTolerance)
                Clear(); // Older readings would otherwise look like the future and never expire.

            if (_newest is null || time > _newest)
                _newest = time;

            if (!_disks.TryGetValue(reading.Sample.DiskNumber, out var queue))
                _disks[reading.Sample.DiskNumber] = queue = new Queue<DiskReading>();
            queue.Enqueue(reading);
            while (queue.Count > _maxPerDisk)
                queue.Dequeue();
        }

        Prune();
    }

    /// <summary>Readings of one disk from the last <paramref name="window"/>, oldest first.</summary>
    public IReadOnlyList<DiskReading> ForDisk(int diskNumber, TimeSpan window)
    {
        if (_newest is not { } newest || !_disks.TryGetValue(diskNumber, out var queue))
            return [];

        var start = newest - window;
        return queue.Where(r => r.Sample.Timestamp >= start).ToList();
    }

    /// <summary>Every reading kept, ordered by time and then by disk number.</summary>
    public IReadOnlyList<DiskReading> All() =>
        _disks.Values
            .SelectMany(q => q)
            .OrderBy(r => r.Sample.Timestamp)
            .ThenBy(r => r.Sample.DiskNumber)
            .ToList();

    public void Clear()
    {
        _disks.Clear();
        _newest = null;
    }

    private void Prune()
    {
        if (_newest is not { } newest)
            return;

        var cutoff = newest - Retention;
        foreach (var (disk, queue) in _disks.ToList())
        {
            while (queue.Count > 0 && queue.Peek().Sample.Timestamp < cutoff)
                queue.Dequeue();
            if (queue.Count == 0)
                _disks.Remove(disk); // A disk that was unplugged long enough ago.
        }
    }
}
