using DiskPulse.Core;

namespace DiskPulse.Core.Tests;

public class DiskInfoCacheTests
{
    private sealed class FakeProvider : IDiskInfoProvider
    {
        public Dictionary<int, DiskInfo> Disks { get; } = new();
        public int Calls { get; private set; }

        public IReadOnlyDictionary<int, DiskInfo> GetDisks()
        {
            Calls++;
            return new Dictionary<int, DiskInfo>(Disks);
        }
    }

    private static DiskSample Sample(int disk, string instance) =>
        new(disk, instance, DateTimeOffset.UnixEpoch, 0, 0, 0, 0, 0, 0, 0);

    [Fact]
    public void KnownDisk_IsResolvedWithoutRequerying()
    {
        var provider = new FakeProvider();
        provider.Disks[0] = new DiskInfo(0, "Samsung SSD", DiskType.Ssd, ['C']);
        var cache = new DiskInfoCache(provider);

        for (int i = 0; i < 5; i++)
            Assert.Equal("Samsung SSD", cache.Resolve(Sample(0, "0 C:")).Model);

        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public void NewDisk_TriggersOneRequery()
    {
        var provider = new FakeProvider();
        var cache = new DiskInfoCache(provider);
        _ = cache.Disks;

        provider.Disks[1] = new DiskInfo(1, "USB HDD", DiskType.Hdd, ['E']);

        Assert.Equal("USB HDD", cache.Resolve(Sample(1, "1 E:")).Model);
        Assert.Equal(2, provider.Calls);
    }

    [Fact]
    public void UndescribedDisk_FallsBack_AndIsNotRequeriedEveryTime()
    {
        var provider = new FakeProvider();
        var cache = new DiskInfoCache(provider);

        DiskInfo info = null!;
        for (int i = 0; i < 5; i++)
            info = cache.Resolve(Sample(2, "2 F: G:"));

        Assert.Equal(DiskInfo.UnknownModel, info.Model);
        Assert.Equal(['F', 'G'], info.DriveLetters);
        Assert.Equal(2, provider.Calls); // Initial load plus one retry for disk 2.
    }
}
