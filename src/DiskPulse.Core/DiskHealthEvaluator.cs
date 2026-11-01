namespace DiskPulse.Core;

/// <summary>
/// Applies <see cref="DiskThresholds"/> to a stream of samples, tracking per disk how long each
/// condition has held. Feed it every sample, in timestamp order.
/// </summary>
public sealed class DiskHealthEvaluator(DiskThresholds? thresholds = null)
{
    /// <summary>
    /// A longer gap between samples (sleep/resume, a stalled tick) breaks every condition, so a
    /// single slow sample after waking does not count as hours of sustained trouble.
    /// </summary>
    public static readonly TimeSpan MaxSampleGap = TimeSpan.FromSeconds(15);

    private readonly Dictionary<int, DiskState> _states = new();

    public DiskThresholds Thresholds { get; set; } = thresholds ?? DiskThresholds.Default;

    public DiskStatus Evaluate(DiskSample sample)
    {
        if (!_states.TryGetValue(sample.DiskNumber, out var state))
            _states[sample.DiskNumber] = state = new DiskState();

        // Rate counters average the interval since the previous sample, so a condition seen in this
        // sample has held since the previous timestamp.
        var intervalStart = sample.Timestamp;
        if (state.LastTimestamp is { } last && last < sample.Timestamp)
        {
            if (sample.Timestamp - last > MaxSampleGap)
                state.Reset();
            else
                intervalStart = last;
        }
        state.LastTimestamp = sample.Timestamp;

        var t = Thresholds;
        var now = sample.Timestamp;
        bool busy = state.Busy.Update(
            sample.ActivePercent >= t.BusyActivePercent && sample.TotalMBps < t.BusyMaxThroughputMBps,
            intervalStart, now, t.BusyDuration);
        bool verySlow = state.VerySlow.Update(sample.ResponseMs > t.VerySlowResponseMs, intervalStart, now, t.VerySlowResponseDuration);
        bool slow = state.Slow.Update(sample.ResponseMs > t.SlowResponseMs, intervalStart, now, t.SlowResponseDuration);
        bool queue = state.Queue.Update(sample.QueueLength > t.HighQueueLength, intervalStart, now, t.HighQueueDuration);

        var warnings = new List<DiskWarning>(3);
        if (busy)
            warnings.Add(DiskWarning.BusyNotMoving);
        if (verySlow)
            warnings.Add(DiskWarning.VerySlowResponse);
        else if (slow)
            warnings.Add(DiskWarning.SlowResponse); // Redundant next to VerySlowResponse.
        if (queue)
            warnings.Add(DiskWarning.HighQueue);

        var health = warnings.Count == 0 ? DiskHealth.Ok : warnings.Max(w => w.Severity());
        return new DiskStatus(sample.DiskNumber, health, warnings);
    }

    /// <summary>Drops the state of disks that are no longer reported.</summary>
    public void Retain(IEnumerable<int> diskNumbers)
    {
        var keep = diskNumbers.ToHashSet();
        foreach (int number in _states.Keys.Where(n => !keep.Contains(n)).ToList())
            _states.Remove(number);
    }

    private sealed class DiskState
    {
        public DateTimeOffset? LastTimestamp;
        public readonly SustainedCondition Slow = new();
        public readonly SustainedCondition VerySlow = new();
        public readonly SustainedCondition Queue = new();
        public readonly SustainedCondition Busy = new();

        public void Reset()
        {
            Slow.Reset();
            VerySlow.Reset();
            Queue.Reset();
            Busy.Reset();
        }
    }

    private sealed class SustainedCondition
    {
        private DateTimeOffset? _since;

        public bool Update(bool holds, DateTimeOffset intervalStart, DateTimeOffset now, TimeSpan duration)
        {
            if (!holds)
            {
                _since = null;
                return false;
            }

            _since ??= intervalStart;
            return now - _since.Value >= duration;
        }

        public void Reset() => _since = null;
    }
}
