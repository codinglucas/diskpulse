namespace DiskPulse.Core.History;

/// <summary>A point on the sparkline, both axes from 0 to 1. X = 1 is the newest reading, Y = 0 the bottom.</summary>
public readonly record struct SparklinePoint(double X, double Y);

/// <param name="Segments">Lines to draw. A gap in the readings (sleep, hang) splits the line instead of bridging it.</param>
/// <param name="Peak">Highest value in the window, for the caption.</param>
/// <param name="Scale">Value at the top of the graph.</param>
/// <param name="ThresholdY">Height of the warning line, or null when there is no threshold.</param>
public sealed record SparklineData(
    IReadOnlyList<IReadOnlyList<SparklinePoint>> Segments,
    double Peak,
    double Scale,
    double? ThresholdY)
{
    public static SparklineData Empty { get; } = new([], 0, 1, null);
}

public static class SparklineBuilder
{
    /// <summary>
    /// Scales values to the graph. The top is the warning threshold, or the peak when it is higher,
    /// so a healthy disk draws a low flat line and the graph shows how close it gets to trouble.
    /// </summary>
    public static SparklineData Build(
        IEnumerable<(DateTimeOffset Time, double Value)> values,
        DateTimeOffset end,
        TimeSpan window,
        double threshold,
        TimeSpan maxGap)
    {
        if (window <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(window), "Window must be positive.");

        var points = values
            .Where(v => v.Time <= end && end - v.Time <= window)
            .OrderBy(v => v.Time)
            .Select(v => (v.Time, Value: Units.Sanitize(v.Value)))
            .ToList();
        if (points.Count == 0)
            return SparklineData.Empty;

        double peak = points.Max(p => p.Value);
        bool hasThreshold = double.IsFinite(threshold) && threshold > 0;
        double scale = Math.Max(peak, hasThreshold ? threshold : 0);
        if (scale <= 0)
            scale = 1; // All zeros: draw along the bottom.

        var segments = new List<IReadOnlyList<SparklinePoint>>();
        var current = new List<SparklinePoint>();
        DateTimeOffset? previous = null;
        foreach (var (time, value) in points)
        {
            if (previous is { } prev && time - prev > maxGap && current.Count > 0)
            {
                segments.Add(current);
                current = new List<SparklinePoint>();
            }

            double x = 1 - (end - time).TotalSeconds / window.TotalSeconds;
            current.Add(new SparklinePoint(Math.Clamp(x, 0, 1), Math.Clamp(value / scale, 0, 1)));
            previous = time;
        }
        segments.Add(current);

        return new SparklineData(segments, peak, scale, hasThreshold ? threshold / scale : null);
    }
}
