namespace DiskPulse.Core;

public enum DiskType
{
    Unknown,
    Hdd,
    Ssd,
    Nvme,
}

public static class DiskTypeExtensions
{
    public static string ToDisplayName(this DiskType type) => type switch
    {
        DiskType.Hdd => "HDD",
        DiskType.Ssd => "SSD",
        DiskType.Nvme => "NVMe",
        _ => "Unknown",
    };
}

/// <summary>Maps the numeric storage WMI properties to a <see cref="DiskType"/>.</summary>
public static class DiskTypeClassifier
{
    /// <summary>MSFT_Disk / MSFT_PhysicalDisk BusType value for NVMe.</summary>
    public const ushort BusTypeNvme = 17;

    /// <summary>MSFT_PhysicalDisk MediaType values.</summary>
    public const ushort MediaTypeHdd = 3;
    public const ushort MediaTypeSsd = 4;
    public const ushort MediaTypeScm = 5;

    /// <summary>MSFT_PhysicalDisk SpindleSpeed when the drive does not report it.</summary>
    public const uint SpindleSpeedUnknown = uint.MaxValue;

    public static DiskType Classify(ushort? busType, ushort? mediaType, uint? spindleSpeed)
    {
        if (busType == BusTypeNvme)
            return DiskType.Nvme;

        if (mediaType is MediaTypeSsd or MediaTypeScm)
            return DiskType.Ssd;
        if (mediaType == MediaTypeHdd)
            return DiskType.Hdd;

        // MediaType is often "Unspecified" behind USB bridges and some RAID drivers;
        // spindle speed is the next best hint.
        if (spindleSpeed == 0)
            return DiskType.Ssd;
        if (spindleSpeed is > 0 and < SpindleSpeedUnknown)
            return DiskType.Hdd;

        return DiskType.Unknown;
    }
}
