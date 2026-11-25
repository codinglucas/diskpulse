using DiskPulse.Core.Logging;
using static DiskPulse.Core.Tests.TestReadings;

namespace DiskPulse.Core.Tests;

public sealed class CsvDiskLoggerTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "DiskPulseTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    private CsvDiskLogger Create(long maxFileBytes = 1_000_000, int filesToKeep = 10) =>
        new(new CsvLogOptions(_directory, maxFileBytes, filesToKeep));

    private string[] Files(string pattern = "*") =>
        Directory.GetFiles(_directory, pattern).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray()!;

    [Fact]
    public void WritesOneFilePerDisk_WithHeaderAndRows()
    {
        using (var logger = Create())
        {
            logger.Write([At(0, disk: 0), At(0, disk: 1)]);
            logger.Write([At(1, disk: 0), At(1, disk: 1)]);
        }

        Assert.Equal(["DiskPulse_disk0_20261124_153000.csv", "DiskPulse_disk1_20261124_153000.csv"], Files());
        string[] lines = File.ReadAllLines(Path.Combine(_directory, Files()[0]));
        Assert.Equal(CsvFormat.Header, lines[0]);
        Assert.Equal(3, lines.Length);
        Assert.StartsWith("2026-11-24T15:30:01.000-03:00,0,", lines[2]);
    }

    [Fact]
    public void RowsAreOnDiskAfterEachWrite()
    {
        using var logger = Create();
        logger.Write([At(0)]);

        using var reader = new StreamReader(new FileStream(logger.CurrentFiles[0], FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete));
        Assert.Equal(2, reader.ReadToEnd().Split(CsvFormat.NewLine, StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public void StartsANewFileWhenFull_AndKeepsOnlyTheNewest()
    {
        using (var logger = Create(maxFileBytes: 400, filesToKeep: 3))
        {
            for (int s = 0; s < 40; s++)
                logger.Write([At(s)]);
        }

        string[] files = Files();
        Assert.Equal(3, files.Length);
        Assert.All(files, f => Assert.True(new FileInfo(Path.Combine(_directory, f)).Length < 400 + 300));
        string newest = File.ReadAllText(Path.Combine(_directory, files[^1]));
        Assert.Contains("2026-11-24T15:30:39.000", newest); // The last row lands in the newest file.
        Assert.DoesNotContain("2026-11-24T15:30:00.000", string.Concat(files.Select(f => File.ReadAllText(Path.Combine(_directory, f)))));
    }

    [Fact]
    public void NeverOverwritesAFileFromTheSameSecond()
    {
        using (var first = Create())
            first.Write([At(0)]);
        using (var second = Create())
            second.Write([At(0)]);

        Assert.Equal(["DiskPulse_disk0_20261124_153000.csv", "DiskPulse_disk0_20261124_153000_2.csv"], Files());
    }

    [Fact]
    public void LeavesOtherFilesAlone()
    {
        Directory.CreateDirectory(_directory);
        string[] others = ["notes.csv", "DiskPulse_disk0_backup.csv", "DiskPulse_disk00_20200101_000000.csv.bak", "DiskPulse_disk1_20200101_000000.csv"];
        foreach (string name in others)
            File.WriteAllText(Path.Combine(_directory, name), "keep me");

        using (var logger = Create(maxFileBytes: 300, filesToKeep: 1))
        {
            for (int s = 0; s < 10; s++)
                logger.Write([At(s, disk: 0)]);
        }

        Assert.All(others, name => Assert.Equal("keep me", File.ReadAllText(Path.Combine(_directory, name))));
        Assert.Single(Files("DiskPulse_disk0_2026*.csv"));
    }

    [Fact]
    public void CreatesTheFolderWhenMissing()
    {
        using var logger = Create();

        logger.Write([At(0)]);

        Assert.True(Directory.Exists(_directory));
    }
}
