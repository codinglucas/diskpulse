using DiskPulse.Core;

namespace DiskPulse.Core.Tests;

public class UnitsTests
{
    [Theory]
    [InlineData(0.0123, 12.3)]
    [InlineData(0.2, 200)]
    [InlineData(0, 0)]
    public void SecondsToMilliseconds_Converts(double seconds, double expectedMs) =>
        Assert.Equal(expectedMs, Units.SecondsToMilliseconds(seconds), precision: 9);

    [Theory]
    [InlineData(1048576, 1)]
    [InlineData(52428800, 50)]
    [InlineData(524288, 0.5)]
    public void BytesToMegabytes_UsesBinaryMegabytes(double bytes, double expectedMb) =>
        Assert.Equal(expectedMb, Units.BytesToMegabytes(bytes), precision: 9);

    [Theory]
    [InlineData(97, 3)]
    [InlineData(100, 0)]
    [InlineData(0, 100)]
    [InlineData(120, 0)]  // % Idle Time can overshoot 100 between samples.
    [InlineData(-5, 100)]
    public void ActivePercentFromIdle_IsClampedTo0Through100(double idle, double expectedActive) =>
        Assert.Equal(expectedActive, Units.ActivePercentFromIdle(idle), precision: 9);

    [Fact]
    public void ActivePercentFromIdle_NaN_IsZero() =>
        Assert.Equal(0, Units.ActivePercentFromIdle(double.NaN));

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-0.001)]
    public void InvalidCounterValues_BecomeZero(double value)
    {
        Assert.Equal(0, Units.Sanitize(value));
        Assert.Equal(0, Units.SecondsToMilliseconds(value));
        Assert.Equal(0, Units.BytesToMegabytes(value));
    }
}
