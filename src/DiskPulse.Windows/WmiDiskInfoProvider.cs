using System.Globalization;
using System.Management;
using System.Runtime.InteropServices;
using DiskPulse.Core;

namespace DiskPulse.Windows;

/// <summary>
/// Reads disk model, type and drive letters from the Storage Management WMI provider.
/// Class and property names are not localized and the enums are numeric, so this works on
/// any display language. Does not require administrator rights.
/// </summary>
public sealed class WmiDiskInfoProvider : IDiskInfoProvider
{
    private const string StorageNamespace = @"\\.\root\Microsoft\Windows\Storage";

    public IReadOnlyDictionary<int, DiskInfo> GetDisks()
    {
        var disks = new Dictionary<int, DiskInfo>();
        try
        {
            var scope = new ManagementScope(StorageNamespace);
            scope.Connect();

            var physical = new Dictionary<int, (ushort? MediaType, uint? SpindleSpeed, ushort? BusType)>();
            ForEach(scope, "SELECT DeviceId, MediaType, SpindleSpeed, BusType FROM MSFT_PhysicalDisk", o =>
            {
                if (int.TryParse(o["DeviceId"] as string, NumberStyles.None, CultureInfo.InvariantCulture, out int id))
                    physical[id] = (AsUInt16(o["MediaType"]), AsUInt32(o["SpindleSpeed"]), AsUInt16(o["BusType"]));
            });

            var letters = new Dictionary<int, SortedSet<char>>();
            ForEach(scope, "SELECT DiskNumber, DriveLetter FROM MSFT_Partition", o =>
            {
                uint? diskNumber = AsUInt32(o["DiskNumber"]);
                char letter = o["DriveLetter"] is { } value ? Convert.ToChar(value, CultureInfo.InvariantCulture) : '\0';
                if (diskNumber is null || !char.IsAsciiLetter(letter))
                    return;

                int key = (int)diskNumber.Value;
                if (!letters.TryGetValue(key, out var set))
                    letters[key] = set = new SortedSet<char>();
                set.Add(char.ToUpperInvariant(letter));
            });

            ForEach(scope, "SELECT Number, FriendlyName, Model, BusType FROM MSFT_Disk", o =>
            {
                if (AsUInt32(o["Number"]) is not { } number)
                    return;

                int key = (int)number;
                physical.TryGetValue(key, out var p);
                string model = FirstNonEmpty(o["FriendlyName"] as string, o["Model"] as string) ?? DiskInfo.UnknownModel;
                var type = DiskTypeClassifier.Classify(AsUInt16(o["BusType"]) ?? p.BusType, p.MediaType, p.SpindleSpeed);
                IReadOnlyList<char> driveLetters = letters.TryGetValue(key, out var set) ? set.ToArray() : Array.Empty<char>();

                disks[key] = new DiskInfo(key, model, type, driveLetters);
            });
        }
        catch (Exception ex) when (ex is ManagementException or COMException or UnauthorizedAccessException)
        {
            // Labels are cosmetic; callers fall back to DiskInfo.Fallback for anything missing.
        }

        return disks;
    }

    private static void ForEach(ManagementScope scope, string wql, Action<ManagementBaseObject> action)
    {
        using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery(wql));
        using var results = searcher.Get();
        foreach (ManagementBaseObject item in results)
        {
            using (item)
                action(item);
        }
    }

    private static ushort? AsUInt16(object? value) =>
        value is null ? null : Convert.ToUInt16(value, CultureInfo.InvariantCulture);

    private static uint? AsUInt32(object? value) =>
        value is null ? null : Convert.ToUInt32(value, CultureInfo.InvariantCulture);

    private static string? FirstNonEmpty(params string?[] values) =>
        values.Select(v => v?.Trim()).FirstOrDefault(v => !string.IsNullOrEmpty(v));
}
