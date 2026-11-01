using DiskPulse.Core;

namespace DiskPulse.Core.Tests;

public class DiskHealthEvaluatorTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    private static DiskSample Sample(
        double seconds,
        int disk = 0,
        double responseMs = 1,
        double activePercent = 5,
        double queue = 0,
        double readMBps = 10,
        double writeMBps = 0) =>
        new(disk, $"{disk} C:", Start.AddSeconds(seconds), responseMs, responseMs, responseMs, activePercent, queue, readMBps, writeMBps);

    /// <summary>Feeds a healthy sample at t=0, then <paramref name="make"/> at t=1..count, returning the last status.</summary>
    private static DiskStatus Run(DiskHealthEvaluator evaluator, int count, Func<double, DiskSample> make)
    {
        evaluator.Evaluate(Sample(0));
        DiskStatus status = null!;
        for (int i = 1; i <= count; i++)
            status = evaluator.Evaluate(make(i));
        return status;
    }

    [Fact]
    public void HealthyDisk_IsOk()
    {
        var status = Run(new DiskHealthEvaluator(), 20, t => Sample(t));

        Assert.Equal(DiskHealth.Ok, status.Health);
        Assert.Empty(status.Warnings);
    }

    [Fact]
    public void SlowResponse_TurnsYellowAfterFiveSeconds()
    {
        var evaluator = new DiskHealthEvaluator();

        Assert.Equal(DiskHealth.Ok, Run(evaluator, 4, t => Sample(t, responseMs: 60)).Health);

        var status = evaluator.Evaluate(Sample(5, responseMs: 60));
        Assert.Equal(DiskHealth.Warning, status.Health);
        Assert.Equal([DiskWarning.SlowResponse], status.Warnings);
    }

    [Fact]
    public void ResponseAtThreshold_IsNotSlow() =>
        Assert.Equal(DiskHealth.Ok, Run(new DiskHealthEvaluator(), 10, t => Sample(t, responseMs: 50)).Health);

    [Fact]
    public void VerySlowResponse_TurnsRed_AndReplacesSlowResponse()
    {
        var status = Run(new DiskHealthEvaluator(), 5, t => Sample(t, responseMs: 250));

        Assert.Equal(DiskHealth.Critical, status.Health);
        Assert.Equal([DiskWarning.VerySlowResponse], status.Warnings);
    }

    [Fact]
    public void VerySlowForLessThanFiveSeconds_AfterSlowPeriod_IsYellow()
    {
        var evaluator = new DiskHealthEvaluator();
        Run(evaluator, 3, t => Sample(t, responseMs: 60));

        DiskStatus status = null!;
        for (int t = 4; t <= 6; t++)
            status = evaluator.Evaluate(Sample(t, responseMs: 250));

        Assert.Equal(DiskHealth.Warning, status.Health);
        Assert.Equal([DiskWarning.SlowResponse], status.Warnings);
    }

    [Fact]
    public void HighQueue_TurnsYellowWhenSustained()
    {
        var status = Run(new DiskHealthEvaluator(), 5, t => Sample(t, queue: 3));

        Assert.Equal(DiskHealth.Warning, status.Health);
        Assert.Equal([DiskWarning.HighQueue], status.Warnings);
    }

    [Fact]
    public void QueueAtThreshold_IsOk() =>
        Assert.Equal(DiskHealth.Ok, Run(new DiskHealthEvaluator(), 10, t => Sample(t, queue: 2)).Health);

    [Fact]
    public void BusyButNotMovingData_TurnsRedAfterTenSeconds()
    {
        var evaluator = new DiskHealthEvaluator();
        Func<double, DiskSample> stuck = t => Sample(t, activePercent: 100, readMBps: 0.3, writeMBps: 0.2);

        Assert.Equal(DiskHealth.Ok, Run(evaluator, 9, stuck).Health);

        var status = evaluator.Evaluate(stuck(10));
        Assert.Equal(DiskHealth.Critical, status.Health);
        Assert.Equal([DiskWarning.BusyNotMoving], status.Warnings);
    }

    [Fact]
    public void BusyAndMovingData_IsOk() =>
        Assert.Equal(DiskHealth.Ok, Run(new DiskHealthEvaluator(), 20, t => Sample(t, activePercent: 100, readMBps: 150)).Health);

    [Fact]
    public void ThroughputCountsReadsAndWrites()
    {
        // 0.6 + 0.6 MB/s is above the 1 MB/s limit even though each direction is below it.
        var status = Run(new DiskHealthEvaluator(), 20, t => Sample(t, activePercent: 95, readMBps: 0.6, writeMBps: 0.6));

        Assert.Equal(DiskHealth.Ok, status.Health);
    }

    [Fact]
    public void SeveralProblems_AreAllReported_MostSevereFirst()
    {
        var status = Run(new DiskHealthEvaluator(), 10,
            t => Sample(t, responseMs: 900, activePercent: 100, queue: 8, readMBps: 0.1, writeMBps: 0));

        Assert.Equal(DiskHealth.Critical, status.Health);
        Assert.Equal([DiskWarning.BusyNotMoving, DiskWarning.VerySlowResponse, DiskWarning.HighQueue], status.Warnings);
    }

    [Fact]
    public void ABreakInTheCondition_RestartsTheTimer()
    {
        var evaluator = new DiskHealthEvaluator();
        Run(evaluator, 4, t => Sample(t, responseMs: 60));
        evaluator.Evaluate(Sample(5, responseMs: 5));

        DiskStatus status = null!;
        for (int t = 6; t <= 9; t++)
            status = evaluator.Evaluate(Sample(t, responseMs: 60));

        Assert.Equal(DiskHealth.Ok, status.Health);
    }

    [Fact]
    public void LongGapBetweenSamples_DoesNotCountAsSustained()
    {
        var evaluator = new DiskHealthEvaluator();
        evaluator.Evaluate(Sample(0, responseMs: 300));
        evaluator.Evaluate(Sample(1, responseMs: 300));

        // e.g. the PC slept for an hour.
        var status = evaluator.Evaluate(Sample(3600, responseMs: 300));

        Assert.Equal(DiskHealth.Ok, status.Health);
    }

    [Fact]
    public void SlowerRefreshRate_StillUsesWallClockDuration()
    {
        var evaluator = new DiskHealthEvaluator();
        evaluator.Evaluate(Sample(0));
        Assert.Equal(DiskHealth.Ok, evaluator.Evaluate(Sample(2.5, responseMs: 60)).Health);
        Assert.Equal(DiskHealth.Warning, evaluator.Evaluate(Sample(5, responseMs: 60)).Health);
    }

    [Fact]
    public void Disks_AreTrackedIndependently()
    {
        var evaluator = new DiskHealthEvaluator();
        evaluator.Evaluate(Sample(0, disk: 0));
        evaluator.Evaluate(Sample(0, disk: 1));

        DiskStatus slow = null!, fine = null!;
        for (int t = 1; t <= 5; t++)
        {
            slow = evaluator.Evaluate(Sample(t, disk: 0, responseMs: 60));
            fine = evaluator.Evaluate(Sample(t, disk: 1));
        }

        Assert.Equal(DiskHealth.Warning, slow.Health);
        Assert.Equal(DiskHealth.Ok, fine.Health);
        Assert.Equal(1, fine.DiskNumber);
    }

    [Fact]
    public void CustomThresholds_AreApplied()
    {
        var thresholds = DiskThresholds.Default with { SlowResponseMs = 20, SlowResponseDuration = TimeSpan.FromSeconds(2) };

        var status = Run(new DiskHealthEvaluator(thresholds), 2, t => Sample(t, responseMs: 25));

        Assert.Equal([DiskWarning.SlowResponse], status.Warnings);
    }

    [Fact]
    public void Retain_ForgetsRemovedDisks()
    {
        var evaluator = new DiskHealthEvaluator();
        Run(evaluator, 4, t => Sample(t, responseMs: 60));

        evaluator.Retain([]);

        // Without the earlier history, one more slow second is not enough.
        Assert.Equal(DiskHealth.Ok, evaluator.Evaluate(Sample(5, responseMs: 60)).Health);
    }

    [Theory]
    [InlineData(DiskWarning.SlowResponse, DiskHealth.Warning)]
    [InlineData(DiskWarning.HighQueue, DiskHealth.Warning)]
    [InlineData(DiskWarning.VerySlowResponse, DiskHealth.Critical)]
    [InlineData(DiskWarning.BusyNotMoving, DiskHealth.Critical)]
    public void Severity(DiskWarning warning, DiskHealth expected) =>
        Assert.Equal(expected, warning.Severity());
}
