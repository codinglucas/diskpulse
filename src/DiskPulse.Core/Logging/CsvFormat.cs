using System.Globalization;
using System.Text;

namespace DiskPulse.Core.Logging;

/// <summary>
/// The CSV layout shared by the per-disk log and the export. Numbers always use "." and no
/// thousands separator, whatever the Windows region, so the files read the same everywhere.
/// </summary>
public static class CsvFormat
{
    public const string NewLine = "\r\n";

    public static readonly string Header = string.Join(',',
        "timestamp", "disk", "drive_letters", "model", "type", "health", "warnings",
        "response_ms", "read_response_ms", "write_response_ms",
        "active_percent", "queue_length", "read_mbps", "write_mbps");

    /// <summary>UTF-8 with a byte-order mark, so Excel shows non-ASCII model names correctly.</summary>
    public static Encoding Encoding { get; } = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    public static string Row(DiskReading reading)
    {
        var (sample, info, status) = reading;
        return string.Join(',',
            sample.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz", CultureInfo.InvariantCulture),
            sample.DiskNumber.ToString(CultureInfo.InvariantCulture),
            Text(string.Join(' ', info.DriveLetters.Select(l => $"{l}:"))),
            Text(info.Model),
            Text(info.Type.ToDisplayName()),
            status.Health.ToString(),
            string.Join(';', status.Warnings),
            Number(sample.ResponseMs),
            Number(sample.ReadResponseMs),
            Number(sample.WriteResponseMs),
            Number(sample.ActivePercent),
            Number(sample.QueueLength),
            Number(sample.ReadMBps),
            Number(sample.WriteMBps));
    }

    /// <summary>Writes a header and one row per reading.</summary>
    public static int Write(TextWriter writer, IEnumerable<DiskReading> readings)
    {
        writer.Write(Header);
        writer.Write(NewLine);
        int rows = 0;
        foreach (var reading in readings)
        {
            writer.Write(Row(reading));
            writer.Write(NewLine);
            rows++;
        }
        return rows;
    }

    /// <summary>
    /// Quotes a text field when needed, and defuses values a spreadsheet would run as a formula
    /// (a disk model reported as "=HYPERLINK(...)" must stay text when the file is opened).
    /// </summary>
    public static string Text(string? value)
    {
        value ??= "";
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
            value = "'" + value;

        bool needsQuotes = value.AsSpan().IndexOfAny(",\"\r\n") >= 0 ||
                           (value.Length > 0 && (char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1])));
        return needsQuotes ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }

    private static string Number(double value) =>
        double.IsFinite(value) ? value.ToString("0.###", CultureInfo.InvariantCulture) : "";
}
