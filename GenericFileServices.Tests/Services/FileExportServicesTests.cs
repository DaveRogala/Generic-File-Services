using System.Collections.Generic;
using System.Text;
using GenericFileServices.Contracts;
using GenericFileServices.Services;
using GenericFileServices.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Moq;

namespace GenericFileServices.Tests.Services;

public class FileExportServicesTests
{
    private readonly Mock<IFileWriterServices> _writerMock = new();
    private readonly Mock<ILogger<FileExportServices<ExportTestDto, TestDbContext>>> _loggerMock = new();
    private readonly IFileExportServices<ExportTestDto, TestDbContext> _sut;

    private const string BasePath = "/exports";
    private const string FileName = "out.csv";
    private const string ConnStr = "connstr";
    private const string Container = "mycontainer";
    private const string BlobPath = "exports/out.csv";

    public FileExportServicesTests()
    {
        _sut = new FileExportServices<ExportTestDto, TestDbContext>(_writerMock.Object, _loggerMock.Object);

        _writerMock
            .Setup(w => w.WriteToBlobAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IEnumerable<ExportTestDto>>(),
                It.IsAny<Encoding>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<int, string>?>(),
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        _writerMock
            .Setup(w => w.ArchiveExistingBlobAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Func<Task<List<ExportTestDto>>> DataProvider(params string[] names) =>
        () => Task.FromResult(names.Select(n => new ExportTestDto(n, 0)).ToList());

    // ── ExportToFileAsync — argument validation ───────────────────────────────

    [Fact]
    public async Task ExportToFileAsync_ThrowsArgumentException_WhenBasePathEmpty()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.ExportToFileAsync("", FileName, DataProvider(), Encoding.UTF8));
    }

    [Fact]
    public async Task ExportToFileAsync_ThrowsArgumentException_WhenFileNameEmpty()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.ExportToFileAsync(BasePath, "", DataProvider(), Encoding.UTF8));
    }

    // ── ExportToFileAsync — happy path ───────────────────────────────────────

    [Fact]
    public async Task ExportToFileAsync_ReturnsEmptyErrors_OnSuccess()
    {
        var errors = await _sut.ExportToFileAsync(
            BasePath, FileName, DataProvider("Alice"), Encoding.UTF8);

        Assert.Empty(errors);
    }

    [Fact]
    public async Task ExportToFileAsync_CallsWriteToFile_WithCorrectPathAndFile()
    {
        await _sut.ExportToFileAsync(BasePath, FileName, DataProvider("Alice"), Encoding.UTF8);

        _writerMock.Verify(w => w.WriteToFile(
            BasePath, FileName,
            It.IsAny<IEnumerable<ExportTestDto>>(),
            Encoding.UTF8, ",",
            It.IsAny<IReadOnlyDictionary<int, string>?>(),
            It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public async Task ExportToFileAsync_PassesRecords_ToWriter()
    {
        List<ExportTestDto>? captured = null;
        _writerMock
            .Setup(w => w.WriteToFile(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IEnumerable<ExportTestDto>>(),
                It.IsAny<Encoding>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<int, string>?>(),
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<string?>()))
            .Callback<string, string, IEnumerable<ExportTestDto>, Encoding, string, IReadOnlyDictionary<int, string>?, bool, bool, string?>(
                (_, _, records, _, _, _, _, _, _) => captured = records.ToList());

        await _sut.ExportToFileAsync(BasePath, FileName, DataProvider("Alice", "Bob"), Encoding.UTF8);

        Assert.NotNull(captured);
        Assert.Equal(2, captured.Count);
        Assert.Equal("Alice", captured[0].Name);
        Assert.Equal("Bob", captured[1].Name);
    }

    [Fact]
    public async Task ExportToFileAsync_ForwardsCustomDelimiter_ToWriter()
    {
        await _sut.ExportToFileAsync(BasePath, FileName, DataProvider("X"), Encoding.UTF8, delimiter: "\t");

        _writerMock.Verify(w => w.WriteToFile(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<IEnumerable<ExportTestDto>>(),
            It.IsAny<Encoding>(), "\t",
            It.IsAny<IReadOnlyDictionary<int, string>?>(),
            It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public async Task ExportToFileAsync_WorksWithEmptyDataSet()
    {
        List<ExportTestDto>? captured = null;
        _writerMock
            .Setup(w => w.WriteToFile(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IEnumerable<ExportTestDto>>(),
                It.IsAny<Encoding>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<int, string>?>(),
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<string?>()))
            .Callback<string, string, IEnumerable<ExportTestDto>, Encoding, string, IReadOnlyDictionary<int, string>?, bool, bool, string?>(
                (_, _, records, _, _, _, _, _, _) => captured = records.ToList());

        var errors = await _sut.ExportToFileAsync(BasePath, FileName, DataProvider(), Encoding.UTF8);

        Assert.Empty(errors);
        Assert.NotNull(captured);
        Assert.Empty(captured);
    }

    // ── ExportToFileAsync — error handling ───────────────────────────────────

    [Fact]
    public async Task ExportToFileAsync_ReturnsErrors_WhenDataProviderThrows()
    {
        Func<Task<List<ExportTestDto>>> failingProvider =
            () => throw new InvalidOperationException("db unavailable");

        var errors = await _sut.ExportToFileAsync(BasePath, FileName, failingProvider, Encoding.UTF8);

        Assert.Single(errors);
        Assert.Contains("db unavailable", errors[0]);
    }

    [Fact]
    public async Task ExportToFileAsync_ReturnsErrors_WhenWriterThrows()
    {
        _writerMock
            .Setup(w => w.WriteToFile(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IEnumerable<ExportTestDto>>(),
                It.IsAny<Encoding>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<int, string>?>(),
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<string?>()))
            .Throws(new IOException("disk full"));

        var errors = await _sut.ExportToFileAsync(
            BasePath, FileName, DataProvider("X"), Encoding.UTF8);

        Assert.Single(errors);
        Assert.Contains("disk full", errors[0]);
    }

    [Fact]
    public async Task ExportToFileAsync_DoesNotCallWriter_WhenDataProviderThrows()
    {
        Func<Task<List<ExportTestDto>>> failingProvider =
            () => throw new InvalidOperationException("db unavailable");

        await _sut.ExportToFileAsync(BasePath, FileName, failingProvider, Encoding.UTF8);

        _writerMock.Verify(w => w.WriteToFile(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<IEnumerable<ExportTestDto>>(),
            It.IsAny<Encoding>(), It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<int, string>?>(),
            It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<string?>()),
            Times.Never);
    }

    // ── ExportToFileAsync — archive existing file ────────────────────────────

    [Fact]
    public async Task ExportToFileAsync_DoesNotCallArchive_WhenFlagFalse()
    {
        await _sut.ExportToFileAsync(BasePath, FileName, DataProvider("X"), Encoding.UTF8);

        _writerMock.Verify(w => w.ArchiveExistingFile(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()),
            Times.Never);
    }

    [Fact]
    public async Task ExportToFileAsync_CallsArchive_WhenFlagTrue()
    {
        await _sut.ExportToFileAsync(
            BasePath, FileName, DataProvider("X"), Encoding.UTF8,
            archiveExistingFile: true);

        _writerMock.Verify(w => w.ArchiveExistingFile(BasePath, FileName, null), Times.Once);
    }

    [Fact]
    public async Task ExportToFileAsync_ForwardsArchivePath_ToWriter()
    {
        await _sut.ExportToFileAsync(
            BasePath, FileName, DataProvider("X"), Encoding.UTF8,
            archiveExistingFile: true, archivePath: "/custom/archive");

        _writerMock.Verify(w => w.ArchiveExistingFile(BasePath, FileName, "/custom/archive"), Times.Once);
    }

    [Fact]
    public async Task ExportToFileAsync_ArchivesBeforeWriting()
    {
        var callOrder = new List<string>();
        _writerMock
            .Setup(w => w.ArchiveExistingFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .Callback(() => callOrder.Add("archive"));
        _writerMock
            .Setup(w => w.WriteToFile(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IEnumerable<ExportTestDto>>(),
                It.IsAny<Encoding>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<int, string>?>(),
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<string?>()))
            .Callback<string, string, IEnumerable<ExportTestDto>, Encoding, string, IReadOnlyDictionary<int, string>?, bool, bool, string?>(
                (_, _, _, _, _, _, _, _, _) => callOrder.Add("write"));

        await _sut.ExportToFileAsync(
            BasePath, FileName, DataProvider("X"), Encoding.UTF8,
            archiveExistingFile: true);

        Assert.Equal(["archive", "write"], callOrder);
    }

    [Fact]
    public async Task ExportToFileAsync_ReturnsErrors_WhenArchiveThrows()
    {
        _writerMock
            .Setup(w => w.ArchiveExistingFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .Throws(new IOException("access denied"));

        var errors = await _sut.ExportToFileAsync(
            BasePath, FileName, DataProvider("X"), Encoding.UTF8,
            archiveExistingFile: true);

        Assert.Single(errors);
        Assert.Contains("access denied", errors[0]);
    }

    // ── ExportToBlobAsync — argument validation ───────────────────────────────

    [Fact]
    public async Task ExportToBlobAsync_ThrowsArgumentException_WhenConnectionStringEmpty()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.ExportToBlobAsync("", Container, BlobPath, DataProvider(), Encoding.UTF8));
    }

    [Fact]
    public async Task ExportToBlobAsync_ThrowsArgumentException_WhenContainerNameEmpty()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.ExportToBlobAsync(ConnStr, "", BlobPath, DataProvider(), Encoding.UTF8));
    }

    [Fact]
    public async Task ExportToBlobAsync_ThrowsArgumentException_WhenBlobPathEmpty()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.ExportToBlobAsync(ConnStr, Container, "", DataProvider(), Encoding.UTF8));
    }

    // ── ExportToBlobAsync — happy path ────────────────────────────────────────

    [Fact]
    public async Task ExportToBlobAsync_ReturnsEmptyErrors_OnSuccess()
    {
        var errors = await _sut.ExportToBlobAsync(
            ConnStr, Container, BlobPath, DataProvider("Alice"), Encoding.UTF8);

        Assert.Empty(errors);
    }

    [Fact]
    public async Task ExportToBlobAsync_CallsWriteToBlobAsync_WithCorrectCoordinates()
    {
        await _sut.ExportToBlobAsync(
            ConnStr, Container, BlobPath, DataProvider("Alice"), Encoding.UTF8);

        _writerMock.Verify(w => w.WriteToBlobAsync(
            ConnStr, Container, BlobPath,
            It.IsAny<IEnumerable<ExportTestDto>>(),
            Encoding.UTF8, ",",
            It.IsAny<IReadOnlyDictionary<int, string>?>(),
            It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public async Task ExportToBlobAsync_PassesRecords_ToWriter()
    {
        List<ExportTestDto>? captured = null;
        _writerMock
            .Setup(w => w.WriteToBlobAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IEnumerable<ExportTestDto>>(),
                It.IsAny<Encoding>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<int, string>?>(),
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<string?>()))
            .Callback<string, string, string, IEnumerable<ExportTestDto>, Encoding, string, IReadOnlyDictionary<int, string>?, bool, bool, string?>(
                (_, _, _, records, _, _, _, _, _, _) => captured = records.ToList())
            .Returns(Task.CompletedTask);

        await _sut.ExportToBlobAsync(
            ConnStr, Container, BlobPath, DataProvider("Alice", "Bob"), Encoding.UTF8);

        Assert.NotNull(captured);
        Assert.Equal(2, captured.Count);
        Assert.Equal("Alice", captured[0].Name);
        Assert.Equal("Bob", captured[1].Name);
    }

    // ── ExportToBlobAsync — error handling ────────────────────────────────────

    [Fact]
    public async Task ExportToBlobAsync_ReturnsErrors_WhenDataProviderThrows()
    {
        Func<Task<List<ExportTestDto>>> failingProvider =
            () => throw new InvalidOperationException("db unavailable");

        var errors = await _sut.ExportToBlobAsync(
            ConnStr, Container, BlobPath, failingProvider, Encoding.UTF8);

        Assert.Single(errors);
        Assert.Contains("db unavailable", errors[0]);
    }

    [Fact]
    public async Task ExportToBlobAsync_ReturnsErrors_WhenWriterThrows()
    {
        _writerMock
            .Setup(w => w.WriteToBlobAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IEnumerable<ExportTestDto>>(),
                It.IsAny<Encoding>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<int, string>?>(),
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<string?>()))
            .ThrowsAsync(new IOException("upload failed"));

        var errors = await _sut.ExportToBlobAsync(
            ConnStr, Container, BlobPath, DataProvider("X"), Encoding.UTF8);

        Assert.Single(errors);
        Assert.Contains("upload failed", errors[0]);
    }

    // ── ExportToBlobAsync — archive existing blob ─────────────────────────────

    [Fact]
    public async Task ExportToBlobAsync_DoesNotCallArchive_WhenFlagFalse()
    {
        await _sut.ExportToBlobAsync(ConnStr, Container, BlobPath, DataProvider("X"), Encoding.UTF8);

        _writerMock.Verify(w => w.ArchiveExistingBlobAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()),
            Times.Never);
    }

    [Fact]
    public async Task ExportToBlobAsync_CallsArchive_WhenFlagTrue()
    {
        await _sut.ExportToBlobAsync(
            ConnStr, Container, BlobPath, DataProvider("X"), Encoding.UTF8,
            archiveExistingBlob: true);

        _writerMock.Verify(w => w.ArchiveExistingBlobAsync(ConnStr, Container, BlobPath, null), Times.Once);
    }

    [Fact]
    public async Task ExportToBlobAsync_ForwardsArchivePath_ToWriter()
    {
        await _sut.ExportToBlobAsync(
            ConnStr, Container, BlobPath, DataProvider("X"), Encoding.UTF8,
            archiveExistingBlob: true, archivePath: "exports/archive");

        _writerMock.Verify(w => w.ArchiveExistingBlobAsync(ConnStr, Container, BlobPath, "exports/archive"), Times.Once);
    }

    [Fact]
    public async Task ExportToBlobAsync_ArchivesBeforeWriting()
    {
        var callOrder = new List<string>();
        _writerMock
            .Setup(w => w.ArchiveExistingBlobAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .Callback(() => callOrder.Add("archive"))
            .Returns(Task.CompletedTask);
        _writerMock
            .Setup(w => w.WriteToBlobAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IEnumerable<ExportTestDto>>(),
                It.IsAny<Encoding>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<int, string>?>(),
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<string?>()))
            .Callback<string, string, string, IEnumerable<ExportTestDto>, Encoding, string, IReadOnlyDictionary<int, string>?, bool, bool, string?>(
                (_, _, _, _, _, _, _, _, _, _) => callOrder.Add("write"))
            .Returns(Task.CompletedTask);

        await _sut.ExportToBlobAsync(
            ConnStr, Container, BlobPath, DataProvider("X"), Encoding.UTF8,
            archiveExistingBlob: true);

        Assert.Equal(["archive", "write"], callOrder);
    }

    [Fact]
    public async Task ExportToBlobAsync_ReturnsErrors_WhenArchiveThrows()
    {
        _writerMock
            .Setup(w => w.ArchiveExistingBlobAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ThrowsAsync(new InvalidOperationException("copy failed"));

        var errors = await _sut.ExportToBlobAsync(
            ConnStr, Container, BlobPath, DataProvider("X"), Encoding.UTF8,
            archiveExistingBlob: true);

        Assert.Single(errors);
        Assert.Contains("copy failed", errors[0]);
    }

    // ── ExportToFileAsync — encoding header ───────────────────────────────────

    [Fact]
    public async Task ExportToFileAsync_ForwardsWriteEncodingHeader_False_ByDefault()
    {
        await _sut.ExportToFileAsync(BasePath, FileName, DataProvider("X"), Encoding.UTF8);

        _writerMock.Verify(w => w.WriteToFile(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<IEnumerable<ExportTestDto>>(),
            It.IsAny<Encoding>(), It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<int, string>?>(),
            It.IsAny<bool>(), false, null),
            Times.Once);
    }

    [Fact]
    public async Task ExportToFileAsync_ForwardsWriteEncodingHeader_True_WhenFlagSet()
    {
        await _sut.ExportToFileAsync(
            BasePath, FileName, DataProvider("X"), Encoding.UTF8,
            writeEncodingHeader: true);

        _writerMock.Verify(w => w.WriteToFile(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<IEnumerable<ExportTestDto>>(),
            It.IsAny<Encoding>(), It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<int, string>?>(),
            It.IsAny<bool>(), true, null),
            Times.Once);
    }

    [Fact]
    public async Task ExportToFileAsync_ForwardsEncodingHeaderOverride_ToWriter()
    {
        await _sut.ExportToFileAsync(
            BasePath, FileName, DataProvider("X"), Encoding.UTF8,
            writeEncodingHeader: true, encodingHeaderOverride: "windows-1252");

        _writerMock.Verify(w => w.WriteToFile(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<IEnumerable<ExportTestDto>>(),
            It.IsAny<Encoding>(), It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<int, string>?>(),
            It.IsAny<bool>(), true, "windows-1252"),
            Times.Once);
    }

    // ── ExportToBlobAsync — encoding header ───────────────────────────────────

    [Fact]
    public async Task ExportToBlobAsync_ForwardsWriteEncodingHeader_False_ByDefault()
    {
        await _sut.ExportToBlobAsync(ConnStr, Container, BlobPath, DataProvider("X"), Encoding.UTF8);

        _writerMock.Verify(w => w.WriteToBlobAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<IEnumerable<ExportTestDto>>(),
            It.IsAny<Encoding>(), It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<int, string>?>(),
            It.IsAny<bool>(), false, null),
            Times.Once);
    }

    [Fact]
    public async Task ExportToBlobAsync_ForwardsWriteEncodingHeader_True_WhenFlagSet()
    {
        await _sut.ExportToBlobAsync(
            ConnStr, Container, BlobPath, DataProvider("X"), Encoding.UTF8,
            writeEncodingHeader: true);

        _writerMock.Verify(w => w.WriteToBlobAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<IEnumerable<ExportTestDto>>(),
            It.IsAny<Encoding>(), It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<int, string>?>(),
            It.IsAny<bool>(), true, null),
            Times.Once);
    }

    [Fact]
    public async Task ExportToBlobAsync_ForwardsEncodingHeaderOverride_ToWriter()
    {
        await _sut.ExportToBlobAsync(
            ConnStr, Container, BlobPath, DataProvider("X"), Encoding.UTF8,
            writeEncodingHeader: true, encodingHeaderOverride: "windows-1252");

        _writerMock.Verify(w => w.WriteToBlobAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<IEnumerable<ExportTestDto>>(),
            It.IsAny<Encoding>(), It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<int, string>?>(),
            It.IsAny<bool>(), true, "windows-1252"),
            Times.Once);
    }

    // ── ExportToFileAsync — write header ─────────────────────────────────────

    [Fact]
    public async Task ExportToFileAsync_ForwardsWriteHeader_True_ByDefault()
    {
        await _sut.ExportToFileAsync(BasePath, FileName, DataProvider("X"), Encoding.UTF8);

        _writerMock.Verify(w => w.WriteToFile(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<IEnumerable<ExportTestDto>>(),
            It.IsAny<Encoding>(), It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<int, string>?>(),
            true, It.IsAny<bool>(), It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public async Task ExportToFileAsync_ForwardsWriteHeader_False_WhenFlagUnset()
    {
        await _sut.ExportToFileAsync(
            BasePath, FileName, DataProvider("X"), Encoding.UTF8,
            writeHeader: false);

        _writerMock.Verify(w => w.WriteToFile(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<IEnumerable<ExportTestDto>>(),
            It.IsAny<Encoding>(), It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<int, string>?>(),
            false, It.IsAny<bool>(), It.IsAny<string?>()),
            Times.Once);
    }

    // ── ExportToBlobAsync — write header ──────────────────────────────────────

    [Fact]
    public async Task ExportToBlobAsync_ForwardsWriteHeader_True_ByDefault()
    {
        await _sut.ExportToBlobAsync(ConnStr, Container, BlobPath, DataProvider("X"), Encoding.UTF8);

        _writerMock.Verify(w => w.WriteToBlobAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<IEnumerable<ExportTestDto>>(),
            It.IsAny<Encoding>(), It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<int, string>?>(),
            true, It.IsAny<bool>(), It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public async Task ExportToBlobAsync_ForwardsWriteHeader_False_WhenFlagUnset()
    {
        await _sut.ExportToBlobAsync(
            ConnStr, Container, BlobPath, DataProvider("X"), Encoding.UTF8,
            writeHeader: false);

        _writerMock.Verify(w => w.WriteToBlobAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<IEnumerable<ExportTestDto>>(),
            It.IsAny<Encoding>(), It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<int, string>?>(),
            false, It.IsAny<bool>(), It.IsAny<string?>()),
            Times.Once);
    }

    // ── ExportToFileAsync — metadata header ─────────────────────────────────

    [Fact]
    public async Task ExportToFileAsync_ForwardsNullMetadataHeader_ByDefault()
    {
        await _sut.ExportToFileAsync(BasePath, FileName, DataProvider("X"), Encoding.UTF8);

        _writerMock.Verify(w => w.WriteToFile(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<IEnumerable<ExportTestDto>>(),
            It.IsAny<Encoding>(), It.IsAny<string>(),
            (IReadOnlyDictionary<int, string>?)null,
            It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public async Task ExportToFileAsync_ForwardsMetadataHeader_ToWriter()
    {
        IReadOnlyDictionary<int, string> metadata = new Dictionary<int, string> { { 1, "meta1" } };
        IReadOnlyDictionary<int, string>? captured = null;

        _writerMock
            .Setup(w => w.WriteToFile(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IEnumerable<ExportTestDto>>(),
                It.IsAny<Encoding>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<int, string>?>(),
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<string?>()))
            .Callback<string, string, IEnumerable<ExportTestDto>, Encoding, string, IReadOnlyDictionary<int, string>?, bool, bool, string?>(
                (_, _, _, _, _, meta, _, _, _) => captured = meta);

        await _sut.ExportToFileAsync(
            BasePath, FileName, DataProvider("X"), Encoding.UTF8,
            metadataHeader: metadata);

        Assert.NotNull(captured);
        Assert.Same(metadata, captured);
    }

    // ── ExportToBlobAsync — metadata header ──────────────────────────────────

    [Fact]
    public async Task ExportToBlobAsync_ForwardsNullMetadataHeader_ByDefault()
    {
        await _sut.ExportToBlobAsync(ConnStr, Container, BlobPath, DataProvider("X"), Encoding.UTF8);

        _writerMock.Verify(w => w.WriteToBlobAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<IEnumerable<ExportTestDto>>(),
            It.IsAny<Encoding>(), It.IsAny<string>(),
            (IReadOnlyDictionary<int, string>?)null,
            It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public async Task ExportToBlobAsync_ForwardsMetadataHeader_ToWriter()
    {
        IReadOnlyDictionary<int, string> metadata = new Dictionary<int, string> { { 1, "meta1" } };
        IReadOnlyDictionary<int, string>? captured = null;

        _writerMock
            .Setup(w => w.WriteToBlobAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IEnumerable<ExportTestDto>>(),
                It.IsAny<Encoding>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<int, string>?>(),
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<string?>()))
            .Callback<string, string, string, IEnumerable<ExportTestDto>, Encoding, string, IReadOnlyDictionary<int, string>?, bool, bool, string?>(
                (_, _, _, _, _, _, meta, _, _, _) => captured = meta)
            .Returns(Task.CompletedTask);

        await _sut.ExportToBlobAsync(
            ConnStr, Container, BlobPath, DataProvider("X"), Encoding.UTF8,
            metadataHeader: metadata);

        Assert.NotNull(captured);
        Assert.Same(metadata, captured);
    }
}
