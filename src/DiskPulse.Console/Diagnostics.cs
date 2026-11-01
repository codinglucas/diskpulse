using System.Globalization;
using System.Runtime.InteropServices;
using DiskPulse.Core;
using DiskPulse.Windows;

namespace DiskPulse.ConsoleApp;

/// <summary>
/// Prints what is needed to confirm DiskPulse works on a given Windows install, in particular
/// on a non-English display language (see docs/localization-check.md).
/// </summary>
internal static class Diagnostics
{
    public static int Run(TimeSpan interval)
    {
        Console.WriteLine("DiskPulse diagnostics");
        Console.WriteLine($"OS:           {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
        Console.WriteLine($"UI language:  {CultureInfo.CurrentUICulture.Name} ({CultureInfo.CurrentUICulture.NativeName})");
        Console.WriteLine($"Culture:      {CultureInfo.CurrentCulture.Name}");
        Console.WriteLine();

        using var source = new PdhDiskMetricsSource();

        Console.WriteLine("Counters (English path -> path Windows resolved for this language):");
        foreach (var (english, localized) in source.GetLocalizedCounterPaths())
        {
            Console.WriteLine($"  {english}");
            Console.WriteLine($"    -> {localized}");
        }
        Console.WriteLine();

        var disks = new DiskInfoCache(new WmiDiskInfoProvider());
        Console.WriteLine($"Disks from WMI: {disks.Disks.Count}");
        foreach (var disk in disks.Disks.Values.OrderBy(d => d.Number))
            Console.WriteLine($"  {disk.Label}");
        Console.WriteLine();

        Thread.Sleep(interval);
        var samples = source.Sample();
        Console.WriteLine($"Sample after {interval.TotalSeconds.ToString(CultureInfo.CurrentCulture)} s:");
        foreach (string line in DiskTable.Format(samples, disks))
            Console.WriteLine($"  {line}");
        Console.WriteLine();

        if (samples.Count == 0)
        {
            Console.WriteLine("Result: FAIL - no physical disk reported counter data.");
            return 1;
        }

        Console.WriteLine($"Result: OK - {samples.Count} disk(s) reporting.");
        return 0;
    }
}
