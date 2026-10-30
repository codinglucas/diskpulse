namespace DiskPulse.Core;

/// <summary>Source of live per-disk metrics.</summary>
public interface IDiskMetricsSource : IDisposable
{
    /// <summary>
    /// Collects one sample per physical disk, ordered by disk number. Rate counters are
    /// averaged over the time since the previous call, so call this once per refresh tick.
    /// </summary>
    IReadOnlyList<DiskSample> Sample();
}

/// <summary>Source of disk labels (model, type, drive letters), keyed by disk number.</summary>
public interface IDiskInfoProvider
{
    IReadOnlyDictionary<int, DiskInfo> GetDisks();
}

/// <summary>Raised when disk metrics cannot be read at all; the message is meant for the user.</summary>
public sealed class DiskMetricsException(string message) : Exception(message);
