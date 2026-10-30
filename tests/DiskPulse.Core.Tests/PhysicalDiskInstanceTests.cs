using DiskPulse.Core;

namespace DiskPulse.Core.Tests;

public class PhysicalDiskInstanceTests
{
    [Theory]
    [InlineData("0 C:", 0, "C")]
    [InlineData("0 C: D:", 0, "CD")]
    [InlineData("1", 1, "")]
    [InlineData("12 e:", 12, "E")]
    [InlineData("  2   F:  ", 2, "F")]
    [InlineData("3 G: \\\\?\\Volume{1234}", 3, "G")]
    public void TryParse_ReadsDiskNumberAndLetters(string instance, int expectedNumber, string expectedLetters)
    {
        Assert.True(PhysicalDiskInstance.TryParse(instance, out int number, out var letters));
        Assert.Equal(expectedNumber, number);
        Assert.Equal(expectedLetters, new string(letters.ToArray()));
    }

    [Theory]
    [InlineData(PhysicalDiskInstance.TotalInstance)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("C:")]
    [InlineData("-1 C:")]
    public void TryParse_RejectsNonDiskInstances(string? instance)
    {
        Assert.False(PhysicalDiskInstance.TryParse(instance, out int number, out var letters));
        Assert.Equal(-1, number);
        Assert.Empty(letters);
    }
}
