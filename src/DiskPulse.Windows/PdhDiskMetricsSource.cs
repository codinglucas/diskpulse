using System.Runtime.InteropServices;
using DiskPulse.Core;
using DiskPulse.Windows.Native;

namespace DiskPulse.Windows;

/// <summary>Reads PhysicalDisk counters through PDH using language-neutral English paths.</summary>
public sealed class PdhDiskMetricsSource : IDiskMetricsSource
{
    private readonly IntPtr _query;
    private readonly Dictionary<string, IntPtr> _counters = new(StringComparer.Ordinal);
    private bool _disposed;

    public PdhDiskMetricsSource()
    {
        uint status = Pdh.PdhOpenQueryW(null, IntPtr.Zero, out _query);
        if (status != Pdh.ErrorSuccess)
            throw new DiskMetricsException($"Could not open a performance counter query (PDH error 0x{status:X8}).");

        try
        {
            foreach (string path in PhysicalDiskCounters.All)
            {
                status = Pdh.PdhAddEnglishCounterW(_query, path, IntPtr.Zero, out IntPtr counter);
                if (status != Pdh.ErrorSuccess)
                    throw new DiskMetricsException(DescribeAddCounterFailure(path, status));
                _counters[path] = counter;
            }

            // Rate counters need two collections before they produce a value; prime the first one.
            // PDH_NO_DATA here just means no disk instances exist yet.
            Pdh.PdhCollectQueryData(_query);
        }
        catch
        {
            Pdh.PdhCloseQuery(_query);
            throw;
        }
    }

    public IReadOnlyList<DiskSample> Sample()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        uint status = Pdh.PdhCollectQueryData(_query);
        var timestamp = DateTimeOffset.Now;
        if (status == Pdh.PdhNoData)
            return Array.Empty<DiskSample>();
        if (status != Pdh.ErrorSuccess)
            throw new DiskMetricsException($"Reading disk counters failed (PDH error 0x{status:X8}).");

        var values = new Dictionary<string, Dictionary<string, double>>(StringComparer.Ordinal);
        foreach (var (path, counter) in _counters)
            values[path] = ReadInstances(counter);

        var samples = new List<DiskSample>();
        foreach (string instance in values[PhysicalDiskCounters.IdleTime].Keys)
        {
            if (!PhysicalDiskInstance.TryParse(instance, out int diskNumber, out _))
                continue; // _Total

            var raw = new RawDiskCounters(
                SecondsPerTransfer: Get(values, PhysicalDiskCounters.SecondsPerTransfer, instance),
                SecondsPerRead: Get(values, PhysicalDiskCounters.SecondsPerRead, instance),
                SecondsPerWrite: Get(values, PhysicalDiskCounters.SecondsPerWrite, instance),
                IdlePercent: Get(values, PhysicalDiskCounters.IdleTime, instance),
                QueueLength: Get(values, PhysicalDiskCounters.QueueLength, instance),
                ReadBytesPerSecond: Get(values, PhysicalDiskCounters.ReadBytesPerSecond, instance),
                WriteBytesPerSecond: Get(values, PhysicalDiskCounters.WriteBytesPerSecond, instance));
            samples.Add(DiskSample.FromRaw(diskNumber, instance, timestamp, raw));
        }

        samples.Sort((a, b) => a.DiskNumber.CompareTo(b.DiskNumber));
        return samples;
    }

    /// <summary>
    /// The counter paths as Windows resolved them for the current display language
    /// (e.g. <c>\Disco Físico(*)\% Tempo Ocioso</c> on pt-BR). Used by diagnostics to prove
    /// that the English paths were translated rather than matched literally.
    /// </summary>
    public IReadOnlyList<(string EnglishPath, string LocalizedPath)> GetLocalizedCounterPaths()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var result = new List<(string, string)>();
        foreach (var (path, counter) in _counters)
            result.Add((path, ReadLocalizedPath(counter)));
        return result;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        Pdh.PdhCloseQuery(_query);
        _disposed = true;
    }

    private static double Get(Dictionary<string, Dictionary<string, double>> values, string path, string instance) =>
        values[path].TryGetValue(instance, out double value) ? value : double.NaN;

    private static Dictionary<string, double> ReadInstances(IntPtr counter)
    {
        var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        // The required size can grow between the two calls if a disk is attached; retry a few times.
        for (int attempt = 0; attempt < 3; attempt++)
        {
            uint size = 0;
            uint status = Pdh.PdhGetFormattedCounterArrayW(counter, Pdh.PdhFmtDouble, ref size, out _, IntPtr.Zero);
            if (status != Pdh.PdhMoreData || size == 0)
                return result; // No instances, or no valid data yet.

            IntPtr buffer = Marshal.AllocHGlobal(checked((int)size));
            try
            {
                status = Pdh.PdhGetFormattedCounterArrayW(counter, Pdh.PdhFmtDouble, ref size, out uint count, buffer);
                if (status == Pdh.PdhMoreData)
                    continue;
                if (status != Pdh.ErrorSuccess)
                    return result;

                int itemSize = Marshal.SizeOf<Pdh.PdhFmtCounterValueItemDouble>();
                for (int i = 0; i < count; i++)
                {
                    var item = Marshal.PtrToStructure<Pdh.PdhFmtCounterValueItemDouble>(buffer + (i * itemSize));
                    if (item.Value.CStatus is not (Pdh.PdhCstatusValidData or Pdh.PdhCstatusNewData))
                        continue;

                    string? name = Marshal.PtrToStringUni(item.Name);
                    if (name is not null)
                        result[name] = item.Value.DoubleValue;
                }
                return result;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        return result;
    }

    private static string ReadLocalizedPath(IntPtr counter)
    {
        uint size = 0;
        uint status = Pdh.PdhGetCounterInfoW(counter, false, ref size, IntPtr.Zero);
        if (status != Pdh.PdhMoreData || size == 0)
            return $"(unavailable, PDH error 0x{status:X8})";

        IntPtr buffer = Marshal.AllocHGlobal(checked((int)size));
        try
        {
            status = Pdh.PdhGetCounterInfoW(counter, false, ref size, buffer);
            if (status != Pdh.ErrorSuccess)
                return $"(unavailable, PDH error 0x{status:X8})";

            var info = Marshal.PtrToStructure<Pdh.PdhCounterInfoHeader>(buffer);
            return Marshal.PtrToStringUni(info.FullPath) ?? "(unavailable)";
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static string DescribeAddCounterFailure(string path, uint status) =>
        status is Pdh.PdhCstatusNoObject or Pdh.PdhCstatusNoCounter
            ? $"Windows could not find the performance counter \"{path}\" (PDH error 0x{status:X8}). " +
              "The performance counter registry may be damaged. Rebuild it by running \"lodctr /R\" " +
              "in a Command Prompt opened as administrator, then try again."
            : $"Could not add the performance counter \"{path}\" (PDH error 0x{status:X8}).";
}
