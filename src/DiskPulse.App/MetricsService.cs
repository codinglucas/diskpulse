using DiskPulse.Core;
using DiskPulse.Windows;

namespace DiskPulse.App;

/// <summary>Everything the overlay needs for one disk on one tick.</summary>
public sealed record DiskReading(DiskSample Sample, DiskInfo Info, DiskStatus Status);

public sealed record MetricsSnapshot(IReadOnlyList<DiskReading> Readings, DiskThresholds Thresholds);

/// <summary>
/// Samples counters, resolves labels and evaluates health on a background thread, then hands each
/// snapshot to <paramref name="onSnapshot"/>. WMI label queries can take a few hundred milliseconds,
/// which is why none of this runs on the UI thread.
/// </summary>
/// <remarks>
/// A failed tick reports the error and the loop carries on, so a temporary problem clears by
/// itself on the next good tick instead of stopping monitoring until restart.
/// </remarks>
public sealed class MetricsService(
    TimeSpan interval,
    DiskThresholds thresholds,
    Action<MetricsSnapshot> onSnapshot,
    Action<string> onError) : IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;

    // Swapped as a whole so the loop never sees an interval from one update and thresholds from another.
    private volatile Options _options = new(interval, thresholds);

    public void Start() => _loop ??= Task.Run(() => RunAsync(_cts.Token));

    /// <summary>Applies new settings from the next tick on. Health history is kept.</summary>
    public void Update(TimeSpan newInterval, DiskThresholds newThresholds) => _options = new Options(newInterval, newThresholds);

    public void Dispose()
    {
        _cts.Cancel();
        try
        {
            _loop?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
        }
        _cts.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        PdhDiskMetricsSource? source = null;
        try
        {
            var disks = new DiskInfoCache(new WmiDiskInfoProvider());
            var options = _options;
            var evaluator = new DiskHealthEvaluator(options.Thresholds);
            using var timer = new PeriodicTimer(options.Interval);
            source = TryCreateSource();

            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                options = _options;
                if (timer.Period != options.Interval)
                    timer.Period = options.Interval;
                evaluator.Thresholds = options.Thresholds;

                try
                {
                    // Recreated after a failure (e.g. damaged counters), so the problem is retried every tick.
                    if (source is null)
                    {
                        source = new PdhDiskMetricsSource();
                        continue; // Its first collection only primes the rate counters.
                    }

                    var samples = source.Sample();
                    evaluator.Retain(samples.Select(s => s.DiskNumber));

                    var readings = samples
                        .Select(s => new DiskReading(s, disks.Resolve(s), evaluator.Evaluate(s)))
                        .ToList();
                    onSnapshot(new MetricsSnapshot(readings, options.Thresholds));
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    onError(ex.Message);
                    source?.Dispose();
                    source = null;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            source?.Dispose();
        }
    }

    private PdhDiskMetricsSource? TryCreateSource()
    {
        try
        {
            return new PdhDiskMetricsSource();
        }
        catch (DiskMetricsException ex)
        {
            onError(ex.Message);
            return null;
        }
    }

    private sealed record Options(TimeSpan Interval, DiskThresholds Thresholds);
}
