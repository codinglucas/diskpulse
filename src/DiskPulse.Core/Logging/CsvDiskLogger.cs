using System.Globalization;
using System.Text.RegularExpressions;

namespace DiskPulse.Core.Logging;

/// <summary>Where and how much to log. Compared by value, so the logger is only rebuilt when these change.</summary>
public sealed record CsvLogOptions(string Directory, long MaxFileBytes, int FilesToKeep);

/// <summary>
/// Appends every reading to one CSV file per disk, starting a new file when the current one
/// reaches <see cref="CsvLogOptions.MaxFileBytes"/> and deleting the oldest beyond
/// <see cref="CsvLogOptions.FilesToKeep"/>. It only ever creates, writes and deletes files named
/// <c>DiskPulse_disk{N}_{yyyyMMdd_HHmmss}.csv</c> inside the chosen folder.
/// </summary>
public sealed partial class CsvDiskLogger(CsvLogOptions options) : IDisposable
{
    private readonly Dictionary<int, LogFile> _files = new();
    private bool _disposed;

    public CsvLogOptions Options { get; } = options;

    /// <summary>Writes one row per reading and flushes. Throws on I/O errors; the caller decides whether to keep logging.</summary>
    public void Write(IEnumerable<DiskReading> readings)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var touched = new HashSet<LogFile>();
        foreach (var reading in readings)
        {
            var file = FileFor(reading.Sample);
            file.WriteLine(CsvFormat.Row(reading));
            touched.Add(file);
        }
        foreach (var file in touched)
            file.Flush();
    }

    /// <summary>Paths of the files currently being written, by disk number.</summary>
    public IReadOnlyDictionary<int, string> CurrentFiles => _files.ToDictionary(f => f.Key, f => f.Value.Path);

    public static string FileName(int diskNumber, DateTimeOffset start, int attempt = 1)
    {
        string name = $"DiskPulse_disk{diskNumber.ToString(CultureInfo.InvariantCulture)}_{start.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)}";
        if (attempt > 1)
            name += "_" + attempt.ToString(CultureInfo.InvariantCulture);
        return name + ".csv";
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        foreach (var file in _files.Values)
            file.Dispose();
        _files.Clear();
    }

    private LogFile FileFor(DiskSample sample)
    {
        if (_files.TryGetValue(sample.DiskNumber, out var file) && file.Bytes < Options.MaxFileBytes)
            return file;

        file?.Dispose();
        file = Create(sample.DiskNumber, sample.Timestamp);
        _files[sample.DiskNumber] = file;
        DeleteOldFiles(sample.DiskNumber);
        return file;
    }

    private LogFile Create(int diskNumber, DateTimeOffset start)
    {
        System.IO.Directory.CreateDirectory(Options.Directory);
        for (int attempt = 1; ; attempt++)
        {
            string path = Path.Combine(Options.Directory, FileName(diskNumber, start, attempt));
            try
            {
                // CreateNew never overwrites, even if two files start in the same second.
                var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read | FileShare.Delete);
                var file = new LogFile(path, stream);
                file.WriteLine(CsvFormat.Header);
                return file;
            }
            catch (IOException) when (File.Exists(path) && attempt < 100)
            {
            }
        }
    }

    private void DeleteOldFiles(int diskNumber)
    {
        var pattern = OwnFilePattern();
        string prefix = $"DiskPulse_disk{diskNumber.ToString(CultureInfo.InvariantCulture)}_";
        var old = System.IO.Directory.EnumerateFiles(Options.Directory, prefix + "*.csv")
            .Select(Path.GetFileName)
            .Where(name => name is not null && pattern.IsMatch(name)) // Never touch files DiskPulse did not create.
            .Order(StringComparer.Ordinal) // The timestamp in the name sorts chronologically.
            .SkipLast(Options.FilesToKeep);

        foreach (string? name in old)
        {
            try
            {
                File.Delete(Path.Combine(Options.Directory, name!));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Open in another program; try again at the next rotation.
            }
        }
    }

    [GeneratedRegex(@"^DiskPulse_disk\d+_\d{8}_\d{6}(_\d+)?\.csv$", RegexOptions.CultureInvariant)]
    private static partial Regex OwnFilePattern();

    private sealed class LogFile(string path, FileStream stream) : IDisposable
    {
        private readonly StreamWriter _writer = new(stream, CsvFormat.Encoding) { NewLine = CsvFormat.NewLine };
        private long _bytes = CsvFormat.Encoding.GetPreamble().Length;

        public string Path { get; } = path;

        /// <summary>Bytes written so far, counted as we go so rotation needs no extra file system calls.</summary>
        public long Bytes => _bytes;

        public void WriteLine(string line)
        {
            _writer.WriteLine(line);
            _bytes += CsvFormat.Encoding.GetByteCount(line) + CsvFormat.NewLine.Length;
        }

        public void Flush() => _writer.Flush();

        public void Dispose()
        {
            try
            {
                _writer.Dispose();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The final flush failed (disk full or removed); nothing more can be done.
            }
        }
    }
}
