using System.Text;
using GenericFileServices.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace GenericFileServices.Tests.Services;

public class FileWriterServicesTests : IDisposable
{
    private readonly FileWriterServices _sut;
    private readonly string _tempDir;

    public FileWriterServicesTests()
    {
        _sut = new FileWriterServices(new Mock<ILogger<FileWriterServices>>().Object);
        _tempDir = Path.Combine(Path.GetTempPath(), $"gfs_writer_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string[] ReadLines(string fileName) =>
        File.ReadAllLines(Path.Combine(_tempDir, fileName));

    private string ReadText(string fileName) =>
        File.ReadAllText(Path.Combine(_tempDir, fileName));

    private byte[] ReadBytes(string fileName) =>
        File.ReadAllBytes(Path.Combine(_tempDir, fileName));

    // ── Header row ───────────────────────────────────────────────────────────

    [Fact]
    public void WriteToFile_WritesHeaderRow_WhenHeadersProvided()
    {
        _sut.WriteToFile(_tempDir, "out.csv", ["Id", "Name"], [], Encoding.UTF8);

        var lines = ReadLines("out.csv");
        Assert.Single(lines);
        Assert.Equal("Id,Name", lines[0]);
    }

    [Fact]
    public void WriteToFile_OmitsHeaderRow_WhenHeadersEmpty()
    {
        _sut.WriteToFile(_tempDir, "out.csv", [], [["1", "Alice"]], Encoding.UTF8);

        var lines = ReadLines("out.csv");
        Assert.Single(lines);
        Assert.Equal("1,Alice", lines[0]);
    }

    // ── Data rows ────────────────────────────────────────────────────────────

    [Fact]
    public void WriteToFile_WritesDataRowsAfterHeader()
    {
        _sut.WriteToFile(_tempDir, "out.csv", ["Id", "Name"],
            [["1", "Alice"], ["2", "Bob"]], Encoding.UTF8);

        var lines = ReadLines("out.csv");
        Assert.Equal(3, lines.Length);
        Assert.Equal("Id,Name", lines[0]);
        Assert.Equal("1,Alice", lines[1]);
        Assert.Equal("2,Bob", lines[2]);
    }

    [Fact]
    public void WriteToFile_CreatesEmptyFile_WhenNoHeadersAndNoRows()
    {
        _sut.WriteToFile(_tempDir, "empty.csv", [], [], Encoding.UTF8);

        Assert.Equal(0, new FileInfo(Path.Combine(_tempDir, "empty.csv")).Length);
    }

    [Fact]
    public void WriteToFile_WritesOnlyHeader_WhenNoDataRows()
    {
        _sut.WriteToFile(_tempDir, "out.csv", ["A", "B"], [], Encoding.UTF8);

        var lines = ReadLines("out.csv");
        Assert.Single(lines);
        Assert.Equal("A,B", lines[0]);
    }

    // ── Field quoting — delimiter ─────────────────────────────────────────────

    [Fact]
    public void WriteToFile_QuotesField_WhenFieldContainsDelimiter()
    {
        _sut.WriteToFile(_tempDir, "out.csv", [], [["key", "a,b"]], Encoding.UTF8);

        Assert.Equal("key,\"a,b\"", ReadLines("out.csv")[0]);
    }

    [Fact]
    public void WriteToFile_DoesNotQuoteField_WhenFieldContainsNoSpecialChars()
    {
        _sut.WriteToFile(_tempDir, "out.csv", [], [["plain"]], Encoding.UTF8);

        Assert.Equal("plain", ReadLines("out.csv")[0]);
    }

    // ── Field quoting — double-quote ──────────────────────────────────────────

    [Fact]
    public void WriteToFile_QuotesAndEscapesField_WhenFieldContainsDoubleQuote()
    {
        _sut.WriteToFile(_tempDir, "out.csv", [], [["say \"hi\""]], Encoding.UTF8);

        Assert.Equal("\"say \"\"hi\"\"\"", ReadLines("out.csv")[0]);
    }

    // ── Field quoting — newline ──────────────────────────────────────────────

    [Fact]
    public void WriteToFile_QuotesField_WhenFieldContainsNewline()
    {
        _sut.WriteToFile(_tempDir, "out.csv", [], [["line1\nline2"]], Encoding.UTF8);

        // File.ReadAllLines splits on \n, so check raw text to see the quoted field intact
        Assert.StartsWith("\"line1\nline2\"", ReadText("out.csv"));
    }

    [Fact]
    public void WriteToFile_QuotesField_WhenFieldContainsCarriageReturn()
    {
        _sut.WriteToFile(_tempDir, "out.csv", [], [["part1\rpart2"]], Encoding.UTF8);

        Assert.StartsWith("\"part1\rpart2\"", ReadText("out.csv"));
    }

    // ── Custom delimiter ─────────────────────────────────────────────────────

    [Fact]
    public void WriteToFile_UsesCustomDelimiter()
    {
        _sut.WriteToFile(_tempDir, "out.tsv", ["A", "B"], [["1", "2"]], Encoding.UTF8, delimiter: "\t");

        var lines = ReadLines("out.tsv");
        Assert.Equal("A\tB", lines[0]);
        Assert.Equal("1\t2", lines[1]);
    }

    [Fact]
    public void WriteToFile_QuotesField_WhenFieldContainsCustomDelimiter()
    {
        _sut.WriteToFile(_tempDir, "out.tsv", [], [["a\tb"]], Encoding.UTF8, delimiter: "\t");

        Assert.Equal("\"a\tb\"", ReadLines("out.tsv")[0]);
    }

    [Fact]
    public void WriteToFile_UsesPipeDelimiter_WhenSpecified()
    {
        _sut.WriteToFile(_tempDir, "out.psv", ["X", "Y"], [["1", "2"]], Encoding.UTF8, delimiter: "|");

        var lines = ReadLines("out.psv");
        Assert.Equal("X|Y", lines[0]);
        Assert.Equal("1|2", lines[1]);
    }

    // ── Encoding ─────────────────────────────────────────────────────────────

    [Fact]
    public void WriteToFile_WritesWithoutBom_WhenUtf8NoBomEncoding()
    {
        var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        _sut.WriteToFile(_tempDir, "out.csv", [], [["Héllo"]], utf8NoBom);

        var bytes = ReadBytes("out.csv");
        Assert.NotEqual(0xEF, bytes[0]);
        Assert.Contains("Héllo", Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public void WriteToFile_WritesBomPrefix_WhenUtf8WithBomEncoding()
    {
        var utf8WithBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        _sut.WriteToFile(_tempDir, "out.csv", [], [["hello"]], utf8WithBom);

        var bytes = ReadBytes("out.csv");
        Assert.Equal(0xEF, bytes[0]);
        Assert.Equal(0xBB, bytes[1]);
        Assert.Equal(0xBF, bytes[2]);
    }

    // ── Multiple rows, multiple fields ───────────────────────────────────────

    [Fact]
    public void WriteToFile_WritesAllFieldsOnEachRow()
    {
        _sut.WriteToFile(_tempDir, "out.csv", ["A", "B", "C"],
            [["1", "2", "3"], ["4", "5", "6"]], Encoding.UTF8);

        var lines = ReadLines("out.csv");
        Assert.Equal("A,B,C", lines[0]);
        Assert.Equal("1,2,3", lines[1]);
        Assert.Equal("4,5,6", lines[2]);
    }
}
