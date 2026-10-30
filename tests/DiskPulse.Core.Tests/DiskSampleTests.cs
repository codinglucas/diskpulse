using DiskPulse.Core;

namespace DiskPulse.Core.Tests;

public class DiskSampleTests
{
    [Fact]
    public void FromRaw_ConvertsEveryCounter()
    {
        var timestamp = new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);
        var raw = new RawDiskCounters(
            SecondsPerTransfer: 0.250,
            SecondsPerRead: 0.300,
            SecondsPerWrite: 0.010,
            IdlePercent: 2,
            QueueLength: 4,
            ReadBytesPerSecond: 524288,
            WriteBytesPerSecond: 0);

        var sample = DiskSample.FromRaw(1, "1 D:", timestamp, raw);

        Assert.Equal(1, sample.DiskNumber);
        Assert.Equal("1 D:", sample.InstanceName);
        Assert.Equal(timestamp, sample.Timestamp);
        Assert.Equal(250, sample.ResponseMs, precision: 9);
        Assert.Equal(300, sample.ReadResponseMs, precision: 9);
        Assert.Equal(10, sample.WriteResponseMs, precision: 9);
        Assert.Equal(98, sample.ActivePercent, precision: 9);
        Assert.Equal(4, sample.QueueLength);
        Assert.Equal(0.5, sample.ReadMBps, precision: 9);
        Assert.Equal(0, sample.WriteMBps);
        Assert.Equal(0.5, sample.TotalMBps, precision: 9);
    }

    [Fact]
    public void FromRaw_MissingCounters_AreZero()
    {
        var raw = new RawDiskCounters(double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN);

        var sample = DiskSample.FromRaw(0, "0 C:", DateTimeOffset.UnixEpoch, raw);

        Assert.Equal(0, sample.ResponseMs);
        Assert.Equal(0, sample.ActivePercent);
        Assert.Equal(0, sample.QueueLength);
        Assert.Equal(0, sample.TotalMBps);
    }
}
