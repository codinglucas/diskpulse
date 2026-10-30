using System.Globalization;

namespace DiskPulse.Core;

/// <summary>
/// Parses PhysicalDisk counter instance names such as <c>"0 C: D:"</c>. Instance names are
/// not localized: they are the disk number followed by the drive letters on that disk.
/// </summary>
public static class PhysicalDiskInstance
{
    public const string TotalInstance = "_Total";

    public static bool TryParse(string? instanceName, out int diskNumber, out IReadOnlyList<char> driveLetters)
    {
        diskNumber = -1;
        driveLetters = Array.Empty<char>();

        if (string.IsNullOrWhiteSpace(instanceName))
            return false;

        string[] parts = instanceName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int number))
            return false;

        var letters = new List<char>();
        foreach (string part in parts.Skip(1))
        {
            if (part.Length == 2 && part[1] == ':' && char.IsAsciiLetter(part[0]))
                letters.Add(char.ToUpperInvariant(part[0]));
        }

        diskNumber = number;
        driveLetters = letters;
        return true;
    }
}
