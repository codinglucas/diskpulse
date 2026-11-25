using DiskPulse.Core.History;
using static DiskPulse.Core.Tests.TestReadings;

namespace DiskPulse.Core.Tests;

public class SampleHistoryTests
{
    [Fact]
    public void DropsReadingsOlderThanTheRetention()
    {
        var history = new SampleHistory(TimeSpan.FromSeconds(10));

        for (int s = 0; s <= 20; s++)
            history.Add([At(s)]);

        var kept = history.ForDisk(0, TimeSpan.FromHours(1));
        Assert.Equal(10, kept[0].Sample.Timestamp.Subtract(Start).TotalSeconds);
        Assert.Equal(11, kept.Count);
    }

    [Fact]
    public void ForDisk_ReturnsOnlyThatDiskAndWindow_OldestFirst()
    {
        var history = new SampleHistory();
        for (int s = 0; s < 120; s++)
            history.Add([At(s, disk: 0), At(s, disk: 1)]);

        var recent = history.ForDisk(1, TimeSpan.FromSeconds(60));

        Assert.Equal(61, recent.Count);
        Assert.All(recent, r => Assert.Equal(1, r.Sample.DiskNumber));
        Assert.True(recent.Zip(recent.Skip(1)).All(p => p.First.Sample.Timestamp < p.Second.Sample.Timestamp));
        Assert.Empty(history.ForDisk(7, TimeSpan.FromSeconds(60)));
    }

    [Fact]
    public void All_OrdersByTimeThenDisk()
    {
        var history = new SampleHistory();
        history.Add([At(0, disk: 2), At(0, disk: 0)]);
        history.Add([At(1, disk: 1)]);

        var all = history.All();

        Assert.Equal([(0, 0), (0, 2), (1, 1)],
            all.Select(r => ((int)(r.Sample.Timestamp - Start).TotalSeconds, r.Sample.DiskNumber)));
    }

    [Fact]
    public void ForgetsADiskOnceItsReadingsExpire()
    {
        var history = new SampleHistory(TimeSpan.FromSeconds(10));
        history.Add([At(0, disk: 5)]);

        history.Add([At(30, disk: 0)]);

        Assert.Equal(1, history.Count);
        Assert.Empty(history.ForDisk(5, TimeSpan.FromHours(1)));
    }

    [Fact]
    public void ClockSetBack_StartsOver()
    {
        var history = new SampleHistory();
        history.Add([At(0), At(1)]);

        history.Add([At(-3600)]);

        Assert.Equal(1, history.Count);
        Assert.Equal(Start.AddHours(-1), history.Newest);
    }

    [Fact]
    public void KeepsAtMostAFewMinutesWorthPerDisk_EvenWithBogusTimestamps()
    {
        var history = new SampleHistory(TimeSpan.FromSeconds(10));

        for (int i = 0; i < 1000; i++)
            history.Add([At(0)]); // All the same instant, so nothing ages out by time.

        Assert.True(history.Count <= 10 / 0.5 + 100);
    }
}
