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
public sealed class MetricsService(
    TimeSpan interval,
    DiskThresholds thresholds,
    Action<MetricsSnapshot> onSnapshot,
    Action<string> onError) : IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;

    public void Start() => _loop ??= Task.Run(() => RunAsync(_cts.Token));

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
        try
        {
            using var source = new PdhDiskMetricsSource();
            var disks = new DiskInfoCache(new WmiDiskInfoProvider());
            var evaluator = new DiskHealthEvaluator(thresholds);
            using var timer = new PeriodicTimer(interval);

            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                var samples = source.Sample();
                evaluator.Retain(samples.Select(s => s.DiskNumber));

                var readings = samples
                    .Select(s => new DiskReading(s, disks.Resolve(s), evaluator.Evaluate(s)))
                    .ToList();
                onSnapshot(new MetricsSnapshot(readings, evaluator.Thresholds));
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (DiskMetricsException ex)
        {
            onError(ex.Message);
        }
    }
}
