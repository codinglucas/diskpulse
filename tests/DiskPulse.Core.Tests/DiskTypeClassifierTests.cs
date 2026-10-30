using DiskPulse.Core;

namespace DiskPulse.Core.Tests;

public class DiskTypeClassifierTests
{
    private const ushort Sata = 11;
    private const ushort Usb = 7;
    private const ushort MediaUnspecified = 0;

    [Theory]
    [InlineData(DiskTypeClassifier.BusTypeNvme, DiskTypeClassifier.MediaTypeSsd, 0u, DiskType.Nvme)]
    [InlineData(DiskTypeClassifier.BusTypeNvme, MediaUnspecified, uint.MaxValue, DiskType.Nvme)]
    [InlineData(Sata, DiskTypeClassifier.MediaTypeSsd, 0u, DiskType.Ssd)]
    [InlineData(Sata, DiskTypeClassifier.MediaTypeScm, 0u, DiskType.Ssd)]
    [InlineData(Sata, DiskTypeClassifier.MediaTypeHdd, 7200u, DiskType.Hdd)]
    [InlineData(Usb, MediaUnspecified, 0u, DiskType.Ssd)]
    [InlineData(Usb, MediaUnspecified, 5400u, DiskType.Hdd)]
    [InlineData(Usb, MediaUnspecified, uint.MaxValue, DiskType.Unknown)]
    public void Classify(ushort busType, ushort mediaType, uint spindleSpeed, DiskType expected) =>
        Assert.Equal(expected, DiskTypeClassifier.Classify(busType, mediaType, spindleSpeed));

    [Fact]
    public void Classify_NothingKnown_IsUnknown() =>
        Assert.Equal(DiskType.Unknown, DiskTypeClassifier.Classify(null, null, null));

    [Theory]
    [InlineData(DiskType.Hdd, "HDD")]
    [InlineData(DiskType.Ssd, "SSD")]
    [InlineData(DiskType.Nvme, "NVMe")]
    [InlineData(DiskType.Unknown, "Unknown")]
    public void ToDisplayName(DiskType type, string expected) =>
        Assert.Equal(expected, type.ToDisplayName());
}
