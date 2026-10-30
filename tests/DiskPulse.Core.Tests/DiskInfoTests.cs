using DiskPulse.Core;

namespace DiskPulse.Core.Tests;

public class DiskInfoTests
{
    [Fact]
    public void Label_IncludesNumberLettersModelAndType()
    {
        var info = new DiskInfo(0, "Samsung SSD 970 EVO Plus 1TB", DiskType.Nvme, ['C', 'D']);

        Assert.Equal("Disk 0  C: D:  Samsung SSD 970 EVO Plus 1TB  [NVMe]", info.Label);
    }

    [Fact]
    public void Label_WithoutDriveLetters_OmitsThem()
    {
        var info = new DiskInfo(2, "ST2000DM008", DiskType.Hdd, []);

        Assert.Equal("Disk 2  ST2000DM008  [HDD]", info.Label);
    }

    [Fact]
    public void Fallback_UsesUnknownModelAndType()
    {
        var info = DiskInfo.Fallback(3, ['E']);

        Assert.Equal("Disk 3  E:  Unknown model  [Unknown]", info.Label);
    }
}
