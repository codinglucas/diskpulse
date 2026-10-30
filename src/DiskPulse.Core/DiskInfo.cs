using System.Text;

namespace DiskPulse.Core;

/// <summary>Static description of a physical disk, used to label its metrics.</summary>
public sealed record DiskInfo(int Number, string Model, DiskType Type, IReadOnlyList<char> DriveLetters)
{
    public const string UnknownModel = "Unknown model";

    /// <summary>Label used when WMI has nothing for this disk; drive letters come from the counter instance name.</summary>
    public static DiskInfo Fallback(int number, IReadOnlyList<char> driveLetters) =>
        new(number, UnknownModel, DiskType.Unknown, driveLetters);

    /// <summary>For example <c>Disk 0  C: D:  Samsung SSD 970 EVO  [NVMe]</c>.</summary>
    public string Label
    {
        get
        {
            var label = new StringBuilder($"Disk {Number}");
            if (DriveLetters.Count > 0)
                label.Append("  ").Append(string.Join(' ', DriveLetters.Select(l => $"{l}:")));
            label.Append("  ").Append(Model);
            label.Append("  [").Append(Type.ToDisplayName()).Append(']');
            return label.ToString();
        }
    }
}
