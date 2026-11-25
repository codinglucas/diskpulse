using System.Globalization;
using DiskPulse.Core.Logging;
using static DiskPulse.Core.Tests.TestReadings;

namespace DiskPulse.Core.Tests;

public class CsvFormatTests
{
    [Fact]
    public void Row_MatchesHeaderAndUsesInvariantNumbers()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR"); // Uses "," for decimals.
        try
        {
            string row = CsvFormat.Row(At(0, disk: 1, responseMs: 1234.5678, health: DiskHealth.Critical,
                warnings: [DiskWarning.VerySlowResponse, DiskWarning.HighQueue]));

            Assert.Equal(
                "2026-11-24T15:30:00.000-03:00,1,C:,Test SSD,NVMe,Critical,VerySlowResponse;HighQueue,1234.568,1234.568,1234.568,12.5,0.25,1.5,2.25",
                row);
            Assert.Equal(CsvFormat.Header.Split(',').Length, row.Split(',').Length);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData("Samsung SSD", "Samsung SSD")]
    [InlineData("A, B", "\"A, B\"")]
    [InlineData("Say \"hi\"", "\"Say \"\"hi\"\"\"")]
    [InlineData("two\nlines", "\"two\nlines\"")]
    [InlineData(" padded ", "\" padded \"")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Text_QuotesWhenNeeded(string? value, string expected) =>
        Assert.Equal(expected, CsvFormat.Text(value));

    [Theory]
    [InlineData("=HYPERLINK(\"http://x\")", "\"'=HYPERLINK(\"\"http://x\"\")\"")]
    [InlineData("+1+2", "'+1+2")]
    [InlineData("-2", "'-2")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("\tcmd", "'\tcmd")]
    public void Text_DefusesSpreadsheetFormulas(string value, string expected) =>
        Assert.Equal(expected, CsvFormat.Text(value));

    [Fact]
    public void Write_AddsHeaderAndCountsRows()
    {
        var writer = new StringWriter();

        int rows = CsvFormat.Write(writer, [At(0), At(1, disk: 1)]);

        string[] lines = writer.ToString().Split(CsvFormat.NewLine);
        Assert.Equal(2, rows);
        Assert.Equal(CsvFormat.Header, lines[0]);
        Assert.Equal(4, lines.Length); // Header, two rows, and the empty string after the last line break.
    }
}
