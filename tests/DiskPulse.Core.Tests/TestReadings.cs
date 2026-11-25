namespace DiskPulse.Core.Tests;

internal static class TestReadings
{
    public static readonly DateTimeOffset Start = new(2026, 11, 24, 15, 30, 0, TimeSpan.FromHours(-3));

    public static DiskReading At(double seconds, int disk = 0, double responseMs = 1, string model = "Test SSD",
        DiskHealth health = DiskHealth.Ok, params DiskWarning[] warnings) =>
        new(
            new DiskSample(disk, $"{disk} C:", Start.AddSeconds(seconds), responseMs, responseMs, responseMs, 12.5, 0.25, 1.5, 2.25),
            new DiskInfo(disk, model, DiskType.Nvme, ['C']),
            new DiskStatus(disk, health, warnings));
}
