using System.Text;
using Azure.Storage.Blobs;
using GenericFileServices.Models;
using GenericFileServices.Services;
using GenericFileServices.Tests.TestHelpers;
using MagellanFileServices.Contracts;
using GenericFileServices.Contracts;
using MagellanFileServices.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace GenericFileServices.Tests.Services;

public class FileReaderServicesTests : IDisposable
{
    private readonly Mock<IFileServices> _fileServicesMock = new();
    private readonly Mock<ILogger<FileReaderServices<TestDto>>> _loggerMock = new();
    private readonly Mock<IBlobClientFactory> _blobClientFactoryMock = new();
    private readonly FileReaderServices<TestDto> _sut;
    private readonly string _tempDir;

    public FileReaderServicesTests()
    {
        _sut = new FileReaderServices<TestDto>(_fileServicesMock.Object, _loggerMock.Object, _blobClientFactoryMock.Object);
        _tempDir = Path.Combine(Path.GetTempPath(), $"gfis_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private static ObjectResult<TestDto> EmptyResult() => new([], []);
    private static ObjectResult<TestDto> ResultWithErrors(params string[] errors) => new([], [.. errors]);
    private static ObjectResult<TestDto> ResultWithData(params string[] names) =>
        new([.. names.Select(n => new TestDto { Name = n })], []);

    // ── ReadFromFile (stream) — no row skip uses bool overload ───────────────

    [Fact]
    public void ReadFromFile_Stream_ReturnsWrappedResultWithFileName()
    {
        var stream = new MemoryStream();
        var data = new List<TestDto> { new() { Name = "Row1" } };
        _fileServicesMock
            .Setup(f => f.GetDataFromFile<TestDto>(stream, Encoding.UTF8, false, ","))
            .Returns(new ObjectResult<TestDto>(data, []));

        var results = _sut.ReadFromFile(stream, "test.csv", Encoding.UTF8, false, ",");

        Assert.Single(results);
        Assert.Equal("test.csv", results[0].FileName);
        Assert.Single(results[0].ObjectResults!);
    }

    [Fact]
    public void ReadFromFile_Stream_UsesRowsToSkipOverload_WhenRowsToSkipNonZero()
    {
        var stream = new MemoryStream();
        _fileServicesMock
            .Setup(f => f.GetDataFromFile<TestDto>(stream, Encoding.UTF8, 3, ",", true))
            .Returns(EmptyResult());

        _sut.ReadFromFile(stream, "test.csv", Encoding.UTF8, false, ",", rowsToSkip: 3, fixUnescapedQuotes: true);

        _fileServicesMock.Verify(
            f => f.GetDataFromFile<TestDto>(stream, Encoding.UTF8, 3, ",", true),
            Times.Once);
    }

    [Fact]
    public void ReadFromFile_Stream_UsesRowsToSkipOverload_WhenFixUnescapedQuotesTrue()
    {
        var stream = new MemoryStream();
        _fileServicesMock
            .Setup(f => f.GetDataFromFile<TestDto>(stream, Encoding.UTF8, 0, ",", true))
            .Returns(EmptyResult());

        _sut.ReadFromFile(stream, "test.csv", Encoding.UTF8, false, ",", rowsToSkip: 0, fixUnescapedQuotes: true);

        _fileServicesMock.Verify(
            f => f.GetDataFromFile<TestDto>(stream, Encoding.UTF8, 0, ",", true),
            Times.Once);
    }

    [Fact]
    public void ReadFromFile_Stream_UsesBoolOverload_WhenNoRowSkip()
    {
        var stream = new MemoryStream();
        _fileServicesMock
            .Setup(f => f.GetDataFromFile<TestDto>(stream, Encoding.UTF8, true, ","))
            .Returns(EmptyResult());

        _sut.ReadFromFile(stream, "test.csv", Encoding.UTF8, firstLineContainsEncoding: true, ",");

        _fileServicesMock.Verify(
            f => f.GetDataFromFile<TestDto>(stream, Encoding.UTF8, true, ","),
            Times.Once);
    }

    [Fact]
    public void ReadFromFile_Stream_ThrowsAndLogs_WhenFileServiceThrows()
    {
        var stream = new MemoryStream();
        _fileServicesMock
            .Setup(f => f.GetDataFromFile<TestDto>(It.IsAny<Stream>(), It.IsAny<Encoding>(), It.IsAny<bool>(), It.IsAny<string>()))
            .Throws(new IOException("stream error"));

        Assert.Throws<IOException>(() =>
            _sut.ReadFromFile(stream, "test.csv", Encoding.UTF8, false));
    }

    // ── ReadFromFile (file system) ───────────────────────────────────────────

    [Fact]
    public void ReadFromFile_File_ThrowsFileNotFoundException_WhenNoFilesAndFailIfMissingTrue()
    {
        Assert.Throws<FileNotFoundException>(() =>
            _sut.ReadFromFile(_tempDir, "*.csv", Encoding.UTF8, failIfFileMissing: true));
    }

    [Fact]
    public void ReadFromFile_File_ReturnsEmpty_WhenNoFilesAndFailIfMissingFalse()
    {
        var results = _sut.ReadFromFile(_tempDir, "*.csv", Encoding.UTF8, failIfFileMissing: false);

        Assert.Empty(results);
    }

    [Fact]
    public void ReadFromFile_File_Throws_WhenMultipleFilesFoundAndMultipleFilesFalse()
    {
        File.WriteAllText(Path.Combine(_tempDir, "a.csv"), "");
        File.WriteAllText(Path.Combine(_tempDir, "b.csv"), "");
        _fileServicesMock
            .Setup(f => f.GetDataFromFile<TestDto>(It.IsAny<string>(), It.IsAny<Encoding>(), It.IsAny<bool>(), It.IsAny<string>()))
            .Returns(EmptyResult());

        Assert.Throws<InvalidOperationException>(() =>
            _sut.ReadFromFile(_tempDir, "*.csv", Encoding.UTF8, multipleFiles: false));
    }

    [Fact]
    public void ReadFromFile_File_ProcessesMultipleFiles_WhenMultipleFilesTrue()
    {
        File.WriteAllText(Path.Combine(_tempDir, "a.csv"), "");
        File.WriteAllText(Path.Combine(_tempDir, "b.csv"), "");
        _fileServicesMock
            .Setup(f => f.GetDataFromFile<TestDto>(It.IsAny<string>(), It.IsAny<Encoding>(), It.IsAny<bool>(), It.IsAny<string>()))
            .Returns(EmptyResult());

        var results = _sut.ReadFromFile(_tempDir, "*.csv", Encoding.UTF8, multipleFiles: true);

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void ReadFromFile_File_ProcessesFilesInLastWriteTimeOrder()
    {
        var first = Path.Combine(_tempDir, "first.csv");
        var second = Path.Combine(_tempDir, "second.csv");
        File.WriteAllText(first, "");
        File.WriteAllText(second, "");
        File.SetLastWriteTimeUtc(first, DateTime.UtcNow.AddMinutes(-10));
        File.SetLastWriteTimeUtc(second, DateTime.UtcNow);

        var callOrder = new List<string>();
        _fileServicesMock
            .Setup(f => f.GetDataFromFile<TestDto>(It.IsAny<string>(), It.IsAny<Encoding>(), It.IsAny<bool>(), It.IsAny<string>()))
            .Returns<string, Encoding, bool, string>((path, _, _, _) =>
            {
                callOrder.Add(Path.GetFileName(path));
                return EmptyResult();
            });

        _sut.ReadFromFile(_tempDir, "*.csv", Encoding.UTF8, multipleFiles: true);

        Assert.Equal("first.csv", callOrder[0]);
        Assert.Equal("second.csv", callOrder[1]);
    }

    [Fact]
    public void ReadFromFile_File_UsesRowsToSkipOverload_WhenRowsToSkipNonZero()
    {
        File.WriteAllText(Path.Combine(_tempDir, "data.csv"), "");
        _fileServicesMock
            .Setup(f => f.GetDataFromFile<TestDto>(It.IsAny<string>(), It.IsAny<Encoding>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(EmptyResult());

        _sut.ReadFromFile(_tempDir, "*.csv", Encoding.UTF8, rowsToSkip: 2, fixUnescapedQuotes: true);

        _fileServicesMock.Verify(
            f => f.GetDataFromFile<TestDto>(It.IsAny<string>(), Encoding.UTF8, 2, ",", true),
            Times.Once);
    }

    [Fact]
    public void ReadFromFile_File_UsesBoolOverload_WhenNoRowSkip()
    {
        File.WriteAllText(Path.Combine(_tempDir, "data.csv"), "");
        _fileServicesMock
            .Setup(f => f.GetDataFromFile<TestDto>(It.IsAny<string>(), It.IsAny<Encoding>(), It.IsAny<bool>(), It.IsAny<string>()))
            .Returns(EmptyResult());

        _sut.ReadFromFile(_tempDir, "*.csv", Encoding.UTF8, firstLineContainsEncoding: true);

        _fileServicesMock.Verify(
            f => f.GetDataFromFile<TestDto>(It.IsAny<string>(), Encoding.UTF8, true, ","),
            Times.Once);
    }

    // ── HandleFileError / HandleFileSuccess (local) ──────────────────────────

    [Fact]
    public void HandleFileError_DelegatesToFileServices()
    {
        var errors = new List<string> { "bad row" };

        _sut.HandleFileError("/base", "file.csv", "something broke", "20260101", errors);

        _fileServicesMock.Verify(
            f => f.HandleFileError("/base", "file.csv", "something broke", "20260101", errors),
            Times.Once);
    }

    [Fact]
    public void HandleFileSuccess_DelegatesToFileServices()
    {
        _sut.HandleFileSuccess("/base", "file.csv", "20260101");

        _fileServicesMock.Verify(
            f => f.HandleFileSuccess("/base", "file.csv", "20260101"),
            Times.Once);
    }

    // ── HandleFileErrorAsync / HandleFileSuccessAsync (blob) ─────────────────

    // Well-known Azurite development connection string — valid format, no real service needed.
    private const string AzuriteConnStr =
        "DefaultEndpointsProtocol=https;AccountName=devstoreaccount1;" +
        "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;" +
        "BlobEndpoint=https://devstoreaccount1.blob.core.windows.net;";

    [Fact]
    public async Task HandleFileErrorAsync_DelegatesToFileServices_WithBlobContainerClient()
    {
        var stream = new MemoryStream();
        var errors = new List<string> { "bad row" };
        var containerClient = new Mock<BlobContainerClient>().Object;
        _blobClientFactoryMock
            .Setup(f => f.GetContainerClient(AzuriteConnStr, "mycontainer"))
            .Returns(containerClient);
        _fileServicesMock
            .Setup(f => f.HandleFileErrorAsync(
                It.IsAny<BlobContainerClient>(), "path/file.csv", "oops", "20260101", errors))
            .Returns(Task.CompletedTask);

        await _sut.HandleFileErrorAsync(stream, AzuriteConnStr, "mycontainer", "path/file.csv", "oops", "20260101", errors);

        _fileServicesMock.Verify(
            f => f.HandleFileErrorAsync(
                It.IsAny<BlobContainerClient>(), "path/file.csv", "oops", "20260101", errors),
            Times.Once);
    }

    [Fact]
    public async Task HandleFileSuccessAsync_DelegatesToFileServices_WithBlobContainerClient()
    {
        var stream = new MemoryStream();
        var containerClient = new Mock<BlobContainerClient>().Object;
        _blobClientFactoryMock
            .Setup(f => f.GetContainerClient(AzuriteConnStr, "mycontainer"))
            .Returns(containerClient);
        _fileServicesMock
            .Setup(f => f.HandleFileSuccessAsync(
                It.IsAny<BlobContainerClient>(), "path/file.csv", "20260101"))
            .Returns(Task.CompletedTask);

        await _sut.HandleFileSuccessAsync(stream, AzuriteConnStr, "mycontainer", "path/file.csv", "20260101");

        _fileServicesMock.Verify(
            f => f.HandleFileSuccessAsync(
                It.IsAny<BlobContainerClient>(), "path/file.csv", "20260101"),
            Times.Once);
    }
}
