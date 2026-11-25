namespace DiskPulse.Core;

/// <summary>Everything known about one disk on one tick: its metrics, its label and its health.</summary>
public sealed record DiskReading(DiskSample Sample, DiskInfo Info, DiskStatus Status);
