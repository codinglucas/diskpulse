namespace DiskPulse.Core;

/// <summary>Converts raw performance-counter values to the units DiskPulse displays.</summary>
public static class Units
{
    /// <summary>Binary megabyte (1024²), matching what Task Manager shows as "MB/s".</summary>
    public const double BytesPerMegabyte = 1024d * 1024d;

    public static double SecondsToMilliseconds(double seconds) => Sanitize(seconds) * 1000d;

    public static double BytesToMegabytes(double bytes) => Sanitize(bytes) / BytesPerMegabyte;

    /// <summary>
    /// Active time as 100 − % Idle Time, clamped to 0–100. "% Disk Time" is not used
    /// because it is derived from queue length and regularly exceeds 100%.
    /// </summary>
    public static double ActivePercentFromIdle(double idlePercent) =>
        double.IsNaN(idlePercent) ? 0d : Math.Clamp(100d - idlePercent, 0d, 100d);

    /// <summary>
    /// Counters can report NaN, infinity or small negatives (first sample, counter wrap,
    /// disk removed mid-interval). None of these are meaningful, so they become 0.
    /// </summary>
    public static double Sanitize(double value) => double.IsFinite(value) && value > 0d ? value : 0d;
}
