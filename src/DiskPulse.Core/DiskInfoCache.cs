namespace DiskPulse.Core;

/// <summary>
/// Caches disk labels and re-queries the provider when a disk number it has never seen shows up,
/// typically a drive plugged in while DiskPulse runs. Each new number triggers at most one
/// re-query, so a disk the provider cannot describe does not cause a query on every tick.
/// </summary>
public sealed class DiskInfoCache(IDiskInfoProvider provider)
{
    private readonly HashSet<int> _queried = new();
    private IReadOnlyDictionary<int, DiskInfo>? _disks;

    public IReadOnlyDictionary<int, DiskInfo> Disks => _disks ??= Load();

    /// <summary>Label for the sample's disk, falling back to the drive letters in its instance name.</summary>
    public DiskInfo Resolve(DiskSample sample)
    {
        if (Disks.TryGetValue(sample.DiskNumber, out var info))
            return info;

        if (_queried.Add(sample.DiskNumber))
        {
            _disks = Load();
            if (_disks.TryGetValue(sample.DiskNumber, out info))
                return info;
        }

        PhysicalDiskInstance.TryParse(sample.InstanceName, out _, out var letters);
        return DiskInfo.Fallback(sample.DiskNumber, letters);
    }

    private IReadOnlyDictionary<int, DiskInfo> Load()
    {
        var disks = provider.GetDisks();
        _queried.UnionWith(disks.Keys);
        return disks;
    }
}
