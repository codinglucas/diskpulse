using DiskPulse.Core.History;
using static DiskPulse.Core.Tests.TestReadings;

namespace DiskPulse.Core.Tests;

public class SparklineBuilderTests
{
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan Gap = TimeSpan.FromSeconds(15);

    private static SparklineData Build(double threshold, params (double Seconds, double Ms)[] values) =>
        SparklineBuilder.Build(values.Select(v => (Start.AddSeconds(v.Seconds), v.Ms)), Start.AddSeconds(60), Window, threshold, Gap);

    [Fact]
    public void Empty_GivesNoSegments() =>
        Assert.Empty(Build(50).Segments);

    [Fact]
    public void HealthyDisk_IsScaledToTheThreshold()
    {
        var data = Build(50, (45, 5), (52.5, 10), (60, 25));

        var points = Assert.Single(data.Segments);
        Assert.Equal([0.75, 0.875, 1], points.Select(p => p.X));
        Assert.Equal([0.1, 0.2, 0.5], points.Select(p => p.Y));
        Assert.Equal(50, data.Scale);
        Assert.Equal(1, data.ThresholdY);
        Assert.Equal(25, data.Peak);
    }

    [Fact]
    public void SpikeAboveTheThreshold_BecomesTheTop()
    {
        var data = Build(50, (50, 10), (60, 200));

        Assert.Equal(200, data.Scale);
        Assert.Equal(0.25, data.ThresholdY);
        Assert.Equal(1, data.Segments[0][^1].Y);
    }

    [Fact]
    public void DropsReadingsOutsideTheWindow()
    {
        var data = Build(50, (-10, 999), (10, 5), (70, 999));

        Assert.Equal(5, data.Peak);
        Assert.Single(Assert.Single(data.Segments));
    }

    [Fact]
    public void GapInReadings_SplitsTheLine()
    {
        var data = Build(50, (0, 1), (1, 1), (30, 1), (31, 1));

        Assert.Equal(2, data.Segments.Count);
    }

    [Fact]
    public void InvalidValues_AreDrawnAsZero()
    {
        var data = Build(0, (59, double.NaN), (60, double.PositiveInfinity));

        Assert.All(data.Segments[0], p => Assert.Equal(0, p.Y));
        Assert.Null(data.ThresholdY);
    }
}
