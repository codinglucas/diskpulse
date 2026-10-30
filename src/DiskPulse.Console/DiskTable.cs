using DiskPulse.Core;

namespace DiskPulse.ConsoleApp;

/// <summary>Formats samples as text. Numbers use the current culture (e.g. "1,5 ms" on pt-BR).</summary>
internal static class DiskTable
{
    public static List<string> Format(IReadOnlyList<DiskSample> samples, IReadOnlyDictionary<int, DiskInfo> disks)
    {
        var lines = new List<string>();
        if (samples.Count == 0)
        {
            lines.Add("No physical disks are reporting data.");
            return lines;
        }

        foreach (var sample in samples)
        {
            lines.Add(LabelFor(sample, disks));
            lines.Add(
                $"  Response {sample.ResponseMs,7:0.0} ms (read {sample.ReadResponseMs:0.0} / write {sample.WriteResponseMs:0.0})" +
                $"   Active {sample.ActivePercent,3:0}%" +
                $"   Queue {sample.QueueLength,2:0}" +
                $"   Read {sample.ReadMBps,7:0.0} MB/s" +
                $"   Write {sample.WriteMBps,7:0.0} MB/s");
        }
        return lines;
    }

    public static string LabelFor(DiskSample sample, IReadOnlyDictionary<int, DiskInfo> disks)
    {
        if (disks.TryGetValue(sample.DiskNumber, out var info))
            return info.Label;

        PhysicalDiskInstance.TryParse(sample.InstanceName, out _, out var letters);
        return DiskInfo.Fallback(sample.DiskNumber, letters).Label;
    }
}
