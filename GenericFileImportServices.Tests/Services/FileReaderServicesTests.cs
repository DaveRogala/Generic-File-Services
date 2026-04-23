using System.Text;
using GenericFileImportServices.Models;
using GenericFileImportServices.Services;
using GenericFileImportServices.Tests.TestHelpers;
using MagellanFileServices.Contracts;
using MagellanFileServices.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace GenericFileImportServices.Tests.Services;

public class FileReaderServicesTests : IDisposable
{
    private readonly Mock<IFileServices> _fileServicesMock = new();
    private readonly Mock<ILogger<FileReaderServices<TestDto>>> _loggerMock = new();
    private readonly FileReaderServices<TestDto> _sut;
    private readonly string _tempDir;

    public FileReaderServicesTests()
    {
        _sut = new FileReaderServices<TestDto>(_fileServicesMock.Object, _loggerMock.Object);
        _tempDir = Path.Combine(Path.GetTempPath(), $"gfis_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private static ObjectResult<TestDto> EmptyResult() =>
        new([], []);

    private static ObjectResult<TestDto> ResultWithErrors(params string[] errors) =>
        new([], [.. errors]);

    private static ObjectResult<TestDto> ResultWithData(params string[] names) =>
        new([.. names.Select(n => new TestDto { Name = n })], []);

    // ── ReadFromFile (stream) ────────────────────────────────────────────────

    [Fact]
    public void ReadFromFile_Stream_ReturnsWrappedResultWithFileName()
    {
        var stream = new MemoryStream();
        var data = new List<TestDto> { new() { Name = "Row1" } };
        _fileServicesMock
            .Setup(f => f.GetDataFromFile<TestDto>(stream, Encoding.UTF8, false, ",", 0, false))
            .Returns(new ObjectResult<TestDto>(data, []));

        var results = _sut.ReadFromFile(stream, "test.csv", Encoding.UTF8, false, ",");

        Assert.Single(results);
        Assert.Equal("test.csv", results[0].FileName);
        Assert.Single(results[0].ObjectResults);
    }

    [Fact]
    public void ReadFromFile_Stream_ForwardsRowsToSkipAndFixUnescapedQuotes()
    {
        var stream = new MemoryStream();
        _fileServicesMock
            .Setup(f => f.GetDataFromFile<TestDto>(stream, Encoding.UTF8, false, ",", 3, true))
            .Returns(EmptyResult());

        _sut.ReadFromFile(stream, "test.csv", Encoding.UTF8, false, ",", rowsToSkip: 3, fixUnescapedQuotes: true);

        _fileServicesMock.Verify(
            f => f.GetDataFromFile<TestDto>(stream, Encoding.UTF8, false, ",", 3, true),
            Times.Once);
    }

    [Fact]
    public void ReadFromFile_Stream_ThrowsAndLogs_WhenFileServiceThrows()
    {
        var stream = new MemoryStream();
        _fileServicesMock
            .Setup(f => f.GetDataFromFile<TestDto>(It.IsAny<Stream>(), It.IsAny<Encoding>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>()))
            .Throws(new IOException("stream error"));

        Assert.Throws<IOException>(() =>
            _sut.ReadFromFile(stream, "test.csv", Encoding.UTF8, false));
    }

    // ── ReadFromFile (file system) ───────────────────────────────────────────

    [Fact]
    public void ReadFromFile_File_ThrowsFileNotFoundException_WhenNoFilesAndFailIfMissingTrue()
    {
        Assert.Throws<Exception>(() =>
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
            .Setup(f => f.GetDataFromFile<TestDto>(It.IsAny<string>(), It.IsAny<Encoding>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>()))
            .Returns(EmptyResult());

        Assert.Throws<Exception>(() =>
            _sut.ReadFromFile(_tempDir, "*.csv", Encoding.UTF8, multipleFiles: false));
    }

    [Fact]
    public void ReadFromFile_File_ProcessesMultipleFiles_WhenMultipleFilesTrue()
    {
        File.WriteAllText(Path.Combine(_tempDir, "a.csv"), "");
        File.WriteAllText(Path.Combine(_tempDir, "b.csv"), "");
        _fileServicesMock
            .Setup(f => f.GetDataFromFile<TestDto>(It.IsAny<string>(), It.IsAny<Encoding>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>()))
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
            .Setup(f => f.GetDataFromFile<TestDto>(It.IsAny<string>(), It.IsAny<Encoding>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>()))
            .Returns<string, Encoding, bool, string, int, bool>((path, _, _, _, _, _) =>
            {
                callOrder.Add(Path.GetFileName(path));
                return EmptyResult();
            });

        _sut.ReadFromFile(_tempDir, "*.csv", Encoding.UTF8, multipleFiles: true);

        Assert.Equal("first.csv", callOrder[0]);
        Assert.Equal("second.csv", callOrder[1]);
    }

    [Fact]
    public void ReadFromFile_File_ForwardsRowsToSkipAndFixUnescapedQuotes()
    {
        File.WriteAllText(Path.Combine(_tempDir, "data.csv"), "");
        _fileServicesMock
            .Setup(f => f.GetDataFromFile<TestDto>(It.IsAny<string>(), It.IsAny<Encoding>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>()))
            .Returns(EmptyResult());

        _sut.ReadFromFile(_tempDir, "*.csv", Encoding.UTF8, rowsToSkip: 2, fixUnescapedQuotes: true);

        _fileServicesMock.Verify(
            f => f.GetDataFromFile<TestDto>(It.IsAny<string>(), Encoding.UTF8, false, ",", 2, true),
            Times.Once);
    }

    // ── HandleFileError / HandleFileSuccess ──────────────────────────────────

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
}
