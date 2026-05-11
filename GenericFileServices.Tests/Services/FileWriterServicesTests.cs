using System.Text;
using GenericFileServices.Services;
using GenericFileServices.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Moq;

namespace GenericFileServices.Tests.Services;

public class FileWriterServicesTests : IDisposable
{
    private readonly IFileWriterServices _sut;
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

    private byte[] ReadBytes(string fileName) =>
        File.ReadAllBytes(Path.Combine(_tempDir, fileName));

    // ── WriteToFile — header and data ────────────────────────────────────────

    [Fact]
    public void WriteToFile_WritesHeaderRowFromPropertyNames()
    {
        _sut.WriteToFile(_tempDir, "out.csv",
            Array.Empty<ExportTestDto>(),
            new UTF8Encoding(false));

        Assert.Equal("Name,Value", ReadLines("out.csv")[0]);
    }

    [Fact]
    public void WriteToFile_WritesDataRows()
    {
        var records = new[] { new ExportTestDto("Alice", 1), new ExportTestDto("Bob", 2) };
        _sut.WriteToFile(_tempDir, "out.csv", records, new UTF8Encoding(false));

        var lines = ReadLines("out.csv");
        Assert.Equal(3, lines.Length);
        Assert.Equal("Name,Value", lines[0]);
        Assert.Equal("Alice,1", lines[1]);
        Assert.Equal("Bob,2", lines[2]);
    }

    [Fact]
    public void WriteToFile_WritesHeaderOnly_WhenNoRecords()
    {
        _sut.WriteToFile(_tempDir, "out.csv",
            Array.Empty<ExportTestDto>(),
            new UTF8Encoding(false));

        var lines = ReadLines("out.csv");
        Assert.Single(lines);
        Assert.Equal("Name,Value", lines[0]);
    }

    // ── WriteToFile — delimiter ──────────────────────────────────────────────

    [Fact]
    public void WriteToFile_UsesCustomDelimiter()
    {
        var records = new[] { new ExportTestDto("Alice", 1) };
        _sut.WriteToFile(_tempDir, "out.tsv", records, new UTF8Encoding(false), delimiter: "\t");

        var lines = ReadLines("out.tsv");
        Assert.Equal("Name\tValue", lines[0]);
        Assert.Equal("Alice\t1", lines[1]);
    }

    [Fact]
    public void WriteToFile_UsesPipeDelimiter_WhenSpecified()
    {
        var records = new[] { new ExportTestDto("Alice", 1) };
        _sut.WriteToFile(_tempDir, "out.psv", records, new UTF8Encoding(false), delimiter: "|");

        var lines = ReadLines("out.psv");
        Assert.Equal("Name|Value", lines[0]);
        Assert.Equal("Alice|1", lines[1]);
    }

    // ── WriteToFile — encoding ───────────────────────────────────────────────

    [Fact]
    public void WriteToFile_WritesWithoutBom_WhenUtf8NoBomEncoding()
    {
        var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        _sut.WriteToFile(_tempDir, "out.csv",
            new[] { new ExportTestDto("Héllo", 1) }, utf8NoBom);

        var bytes = ReadBytes("out.csv");
        Assert.NotEqual(0xEF, bytes[0]);
        Assert.Contains("Héllo", Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public void WriteToFile_WritesBomPrefix_WhenUtf8WithBomEncoding()
    {
        var utf8WithBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        _sut.WriteToFile(_tempDir, "out.csv",
            new[] { new ExportTestDto("hello", 1) }, utf8WithBom);

        var bytes = ReadBytes("out.csv");
        Assert.Equal(0xEF, bytes[0]);
        Assert.Equal(0xBB, bytes[1]);
        Assert.Equal(0xBF, bytes[2]);
    }

    // ── ArchiveExistingFile — no-op ──────────────────────────────────────────

    [Fact]
    public void ArchiveExistingFile_DoesNothing_WhenFileDoesNotExist()
    {
        // Must not throw
        _sut.ArchiveExistingFile(_tempDir, "nonexistent.csv");
    }

    // ── ArchiveExistingFile — default archive path ───────────────────────────

    [Fact]
    public void ArchiveExistingFile_MovesFileToArchiveSubFolder_WhenArchivePathIsNull()
    {
        File.WriteAllText(Path.Combine(_tempDir, "export.csv"), "data");

        _sut.ArchiveExistingFile(_tempDir, "export.csv");

        Assert.False(File.Exists(Path.Combine(_tempDir, "export.csv")));
        Assert.Single(Directory.GetFiles(Path.Combine(_tempDir, "archive"), "export_*.csv"));
    }

    [Fact]
    public void ArchiveExistingFile_CreatesArchiveDirectory_WhenItDoesNotExist()
    {
        File.WriteAllText(Path.Combine(_tempDir, "export.csv"), "data");

        _sut.ArchiveExistingFile(_tempDir, "export.csv");

        Assert.True(Directory.Exists(Path.Combine(_tempDir, "archive")));
    }

    [Fact]
    public void ArchiveExistingFile_PreservesFileContent()
    {
        File.WriteAllText(Path.Combine(_tempDir, "export.csv"), "original content");

        _sut.ArchiveExistingFile(_tempDir, "export.csv");

        var archived = Directory.GetFiles(Path.Combine(_tempDir, "archive"), "export_*.csv");
        Assert.Equal("original content", File.ReadAllText(archived[0]));
    }

    [Fact]
    public void ArchiveExistingFile_PreservesFileExtension()
    {
        File.WriteAllText(Path.Combine(_tempDir, "report.tsv"), "data");

        _sut.ArchiveExistingFile(_tempDir, "report.tsv");

        Assert.Single(Directory.GetFiles(Path.Combine(_tempDir, "archive"), "report_*.tsv"));
    }

    [Fact]
    public void ArchiveExistingFile_ArchivedNameContainsTimestamp_InExpectedFormat()
    {
        File.WriteAllText(Path.Combine(_tempDir, "export.csv"), "data");
        var before = DateTime.UtcNow;

        _sut.ArchiveExistingFile(_tempDir, "export.csv");

        var archived = Directory.GetFiles(Path.Combine(_tempDir, "archive"), "export_*.csv");
        var stem = Path.GetFileNameWithoutExtension(archived[0]);
        var timestampPart = stem["export_".Length..];
        Assert.True(DateTime.TryParseExact(
            timestampPart, "yyyyMMddHHmmssfff",
            null, System.Globalization.DateTimeStyles.None, out var parsed));
        Assert.True(parsed >= before.AddSeconds(-1));
    }

    // ── ArchiveExistingFile — absolute archive path ──────────────────────────

    [Fact]
    public void ArchiveExistingFile_UsesAbsoluteArchivePath_WhenProvided()
    {
        var absoluteArchive = Path.Combine(_tempDir, "custom_archive");
        File.WriteAllText(Path.Combine(_tempDir, "export.csv"), "data");

        _sut.ArchiveExistingFile(_tempDir, "export.csv", absoluteArchive);

        Assert.Single(Directory.GetFiles(absoluteArchive, "export_*.csv"));
    }

    [Fact]
    public void ArchiveExistingFile_CreatesAbsoluteArchiveDirectory_WhenItDoesNotExist()
    {
        var absoluteArchive = Path.Combine(_tempDir, "custom_archive");
        File.WriteAllText(Path.Combine(_tempDir, "export.csv"), "data");

        _sut.ArchiveExistingFile(_tempDir, "export.csv", absoluteArchive);

        Assert.True(Directory.Exists(absoluteArchive));
    }

    // ── ArchiveExistingFile — relative archive path ──────────────────────────

    [Fact]
    public void ArchiveExistingFile_ResolvesRelativeArchivePath_RelativeToBasePath()
    {
        File.WriteAllText(Path.Combine(_tempDir, "export.csv"), "data");

        _sut.ArchiveExistingFile(_tempDir, "export.csv", "old");

        Assert.Single(Directory.GetFiles(Path.Combine(_tempDir, "old"), "export_*.csv"));
    }

    [Fact]
    public void ArchiveExistingFile_CreatesRelativeArchiveDirectory_WhenItDoesNotExist()
    {
        File.WriteAllText(Path.Combine(_tempDir, "export.csv"), "data");

        _sut.ArchiveExistingFile(_tempDir, "export.csv", "old");

        Assert.True(Directory.Exists(Path.Combine(_tempDir, "old")));
    }
}
