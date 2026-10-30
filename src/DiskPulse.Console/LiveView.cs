using DiskPulse.Core;
using DiskPulse.Windows;

namespace DiskPulse.ConsoleApp;

internal static class LiveView
{
    public static async Task<int> RunOnceAsync(TimeSpan interval)
    {
        using var source = new PdhDiskMetricsSource();
        var disks = new WmiDiskInfoProvider().GetDisks();

        // The source primed itself on construction; wait one interval so rates cover a full period.
        await Task.Delay(interval);
        var samples = source.Sample();

        Console.WriteLine($"{DateTimeOffset.Now:G}");
        foreach (string line in DiskTable.Format(samples, disks))
            Console.WriteLine(line);
        return samples.Count > 0 ? 0 : 1;
    }

    public static async Task<int> RunAsync(TimeSpan interval)
    {
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        using var source = new PdhDiskMetricsSource();
        var infoProvider = new WmiDiskInfoProvider();
        var disks = infoProvider.GetDisks();
        bool redirected = Console.IsOutputRedirected;
        int previousLineCount = 0;

        if (!redirected)
        {
            Console.Clear();
            Console.CursorVisible = false;
        }

        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(cts.Token))
            {
                var samples = source.Sample();

                // A disk we have no label for was probably just attached; refresh labels.
                if (samples.Any(s => !disks.ContainsKey(s.DiskNumber)))
                    disks = infoProvider.GetDisks();

                var lines = new List<string> { $"DiskPulse  {DateTimeOffset.Now:G}  (Ctrl+C to exit)", "" };
                lines.AddRange(DiskTable.Format(samples, disks));

                if (redirected)
                {
                    foreach (string line in lines)
                        Console.WriteLine(line);
                    Console.WriteLine();
                }
                else
                {
                    previousLineCount = Redraw(lines, previousLineCount);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (!redirected)
                Console.CursorVisible = true;
        }

        return 0;
    }

    /// <summary>Overwrites the previous frame in place instead of clearing, which avoids flicker.</summary>
    private static int Redraw(List<string> lines, int previousLineCount)
    {
        int width = Math.Max(Console.WindowWidth - 1, 1);
        Console.SetCursorPosition(0, 0);
        foreach (string line in lines)
            Console.WriteLine(Fit(line, width));
        for (int i = lines.Count; i < previousLineCount; i++)
            Console.WriteLine(new string(' ', width));
        return lines.Count;
    }

    private static string Fit(string line, int width) =>
        line.Length >= width ? line[..width] : line.PadRight(width);
}
