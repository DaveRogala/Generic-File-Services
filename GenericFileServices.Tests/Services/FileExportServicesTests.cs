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
    private readonly Mock<ILogger<FileExportServices<TestEntity, TestDbContext>>> _loggerMock = new();
    private readonly FileExportServices<TestEntity, TestDbContext> _sut;

    private const string BasePath = "/exports";
    private const string FileName = "out.csv";
    private const string ConnStr = "connstr";
    private const string Container = "mycontainer";
    private const string BlobPath = "exports/out.csv";

    public FileExportServicesTests()
    {
        _sut = new FileExportServices<TestEntity, TestDbContext>(_writerMock.Object, _loggerMock.Object);

        // Default blob setup — WriteToBlobAsync returns Task.CompletedTask unless overridden
        _writerMock
            .Setup(w => w.WriteToBlobAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<IEnumerable<string>>>(),
                It.IsAny<Encoding>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Func<Task<List<TestEntity>>> DataProvider(params string[] names) =>
        () => Task.FromResult(names.Select(n => new TestEntity { Name = n }).ToList());

    private static Func<TestEntity, IEnumerable<string>> NameMapper =>
        e => [e.Name];

    // Captures the rows passed to WriteToFile by materialising the lazy sequence
    private void CaptureWriteToFileRows(out Func<List<List<string>>> getRows)
    {
        List<List<string>>? captured = null;
        _writerMock
            .Setup(w => w.WriteToFile(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<IEnumerable<string>>>(),
                It.IsAny<Encoding>(), It.IsAny<string>()))
            .Callback<string, string, IEnumerable<string>, IEnumerable<IEnumerable<string>>, Encoding, string>(
                (_, _, _, rows, _, _) => captured = rows.Select(r => r.ToList()).ToList());
        getRows = () => captured!;
    }

    // ── ExportToFileAsync — argument validation ───────────────────────────────

    [Fact]
    public async Task ExportToFileAsync_ThrowsArgumentException_WhenBasePathEmpty()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.ExportToFileAsync("", FileName, DataProvider(), NameMapper, [], Encoding.UTF8));
    }

    [Fact]
    public async Task ExportToFileAsync_ThrowsArgumentException_WhenFileNameEmpty()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.ExportToFileAsync(BasePath, "", DataProvider(), NameMapper, [], Encoding.UTF8));
    }

    // ── ExportToFileAsync — happy path ───────────────────────────────────────

    [Fact]
    public async Task ExportToFileAsync_ReturnsEmptyErrors_OnSuccess()
    {
        var errors = await _sut.ExportToFileAsync(
            BasePath, FileName, DataProvider("Alice"), NameMapper, ["Name"], Encoding.UTF8);

        Assert.Empty(errors);
    }

    [Fact]
    public async Task ExportToFileAsync_CallsWriteToFile_WithCorrectPathAndFile()
    {
        await _sut.ExportToFileAsync(
            BasePath, FileName, DataProvider("Alice"), NameMapper, ["Name"], Encoding.UTF8);

        _writerMock.Verify(w => w.WriteToFile(
            BasePath, FileName,
            It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<IEnumerable<string>>>(),
            Encoding.UTF8, ","),
            Times.Once);
    }

    [Fact]
    public async Task ExportToFileAsync_PassesMappedRows_ToWriter()
    {
        CaptureWriteToFileRows(out var getRows);

        await _sut.ExportToFileAsync(
            BasePath, FileName, DataProvider("Alice", "Bob"), NameMapper, ["Name"], Encoding.UTF8);

        var rows = getRows();
        Assert.Equal(2, rows.Count);
        Assert.Equal(["Alice"], rows[0]);
        Assert.Equal(["Bob"], rows[1]);
    }

    [Fact]
    public async Task ExportToFileAsync_PassesHeaders_ToWriter()
    {
        string[]? capturedHeaders = null;
        _writerMock
            .Setup(w => w.WriteToFile(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<IEnumerable<string>>>(),
                It.IsAny<Encoding>(), It.IsAny<string>()))
            .Callback<string, string, IEnumerable<string>, IEnumerable<IEnumerable<string>>, Encoding, string>(
                (_, _, headers, _, _, _) => capturedHeaders = headers.ToArray());

        await _sut.ExportToFileAsync(
            BasePath, FileName, DataProvider("X"), NameMapper, ["Name"], Encoding.UTF8);

        Assert.Equal(["Name"], capturedHeaders);
    }

    [Fact]
    public async Task ExportToFileAsync_ForwardsCustomDelimiter_ToWriter()
    {
        await _sut.ExportToFileAsync(
            BasePath, FileName, DataProvider("X"), NameMapper, ["Name"], Encoding.UTF8, delimiter: "\t");

        _writerMock.Verify(w => w.WriteToFile(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<IEnumerable<string>>>(),
            It.IsAny<Encoding>(), "\t"),
            Times.Once);
    }

    [Fact]
    public async Task ExportToFileAsync_WorksWithEmptyDataSet()
    {
        CaptureWriteToFileRows(out var getRows);

        var errors = await _sut.ExportToFileAsync(
            BasePath, FileName, DataProvider(), NameMapper, ["Name"], Encoding.UTF8);

        Assert.Empty(errors);
        Assert.Empty(getRows());
    }

    // ── ExportToFileAsync — error handling ───────────────────────────────────

    [Fact]
    public async Task ExportToFileAsync_ReturnsErrors_WhenDataProviderThrows()
    {
        Func<Task<List<TestEntity>>> failingProvider =
            () => throw new InvalidOperationException("db unavailable");

        var errors = await _sut.ExportToFileAsync(
            BasePath, FileName, failingProvider, NameMapper, ["Name"], Encoding.UTF8);

        Assert.Single(errors);
        Assert.Contains("db unavailable", errors[0]);
    }

    [Fact]
    public async Task ExportToFileAsync_ReturnsErrors_WhenWriterThrows()
    {
        _writerMock
            .Setup(w => w.WriteToFile(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<IEnumerable<string>>>(),
                It.IsAny<Encoding>(), It.IsAny<string>()))
            .Throws(new IOException("disk full"));

        var errors = await _sut.ExportToFileAsync(
            BasePath, FileName, DataProvider("X"), NameMapper, ["Name"], Encoding.UTF8);

        Assert.Single(errors);
        Assert.Contains("disk full", errors[0]);
    }

    [Fact]
    public async Task ExportToFileAsync_DoesNotCallWriter_WhenDataProviderThrows()
    {
        Func<Task<List<TestEntity>>> failingProvider =
            () => throw new InvalidOperationException("db unavailable");

        await _sut.ExportToFileAsync(BasePath, FileName, failingProvider, NameMapper, ["Name"], Encoding.UTF8);

        _writerMock.Verify(w => w.WriteToFile(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<IEnumerable<string>>>(),
            It.IsAny<Encoding>(), It.IsAny<string>()),
            Times.Never);
    }

    // ── ExportToBlobAsync — argument validation ───────────────────────────────

    [Fact]
    public async Task ExportToBlobAsync_ThrowsArgumentException_WhenConnectionStringEmpty()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.ExportToBlobAsync("", Container, BlobPath, DataProvider(), NameMapper, [], Encoding.UTF8));
    }

    [Fact]
    public async Task ExportToBlobAsync_ThrowsArgumentException_WhenContainerNameEmpty()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.ExportToBlobAsync(ConnStr, "", BlobPath, DataProvider(), NameMapper, [], Encoding.UTF8));
    }

    [Fact]
    public async Task ExportToBlobAsync_ThrowsArgumentException_WhenBlobPathEmpty()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.ExportToBlobAsync(ConnStr, Container, "", DataProvider(), NameMapper, [], Encoding.UTF8));
    }

    // ── ExportToBlobAsync — happy path ────────────────────────────────────────

    [Fact]
    public async Task ExportToBlobAsync_ReturnsEmptyErrors_OnSuccess()
    {
        var errors = await _sut.ExportToBlobAsync(
            ConnStr, Container, BlobPath, DataProvider("Alice"), NameMapper, ["Name"], Encoding.UTF8);

        Assert.Empty(errors);
    }

    [Fact]
    public async Task ExportToBlobAsync_CallsWriteToBlobAsync_WithCorrectCoordinates()
    {
        await _sut.ExportToBlobAsync(
            ConnStr, Container, BlobPath, DataProvider("Alice"), NameMapper, ["Name"], Encoding.UTF8);

        _writerMock.Verify(w => w.WriteToBlobAsync(
            ConnStr, Container, BlobPath,
            It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<IEnumerable<string>>>(),
            Encoding.UTF8, ","),
            Times.Once);
    }

    [Fact]
    public async Task ExportToBlobAsync_PassesMappedRows_ToWriter()
    {
        List<List<string>>? captured = null;
        _writerMock
            .Setup(w => w.WriteToBlobAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<IEnumerable<string>>>(),
                It.IsAny<Encoding>(), It.IsAny<string>()))
            .Callback<string, string, string, IEnumerable<string>, IEnumerable<IEnumerable<string>>, Encoding, string>(
                (_, _, _, _, rows, _, _) => captured = rows.Select(r => r.ToList()).ToList())
            .Returns(Task.CompletedTask);

        await _sut.ExportToBlobAsync(
            ConnStr, Container, BlobPath, DataProvider("Alice", "Bob"), NameMapper, ["Name"], Encoding.UTF8);

        Assert.NotNull(captured);
        Assert.Equal(2, captured.Count);
        Assert.Equal(["Alice"], captured[0]);
        Assert.Equal(["Bob"], captured[1]);
    }

    // ── ExportToBlobAsync — error handling ────────────────────────────────────

    [Fact]
    public async Task ExportToBlobAsync_ReturnsErrors_WhenDataProviderThrows()
    {
        Func<Task<List<TestEntity>>> failingProvider =
            () => throw new InvalidOperationException("db unavailable");

        var errors = await _sut.ExportToBlobAsync(
            ConnStr, Container, BlobPath, failingProvider, NameMapper, ["Name"], Encoding.UTF8);

        Assert.Single(errors);
        Assert.Contains("db unavailable", errors[0]);
    }

    [Fact]
    public async Task ExportToBlobAsync_ReturnsErrors_WhenWriterThrows()
    {
        _writerMock
            .Setup(w => w.WriteToBlobAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<IEnumerable<string>>>(),
                It.IsAny<Encoding>(), It.IsAny<string>()))
            .ThrowsAsync(new IOException("upload failed"));

        var errors = await _sut.ExportToBlobAsync(
            ConnStr, Container, BlobPath, DataProvider("X"), NameMapper, ["Name"], Encoding.UTF8);

        Assert.Single(errors);
        Assert.Contains("upload failed", errors[0]);
    }
}
