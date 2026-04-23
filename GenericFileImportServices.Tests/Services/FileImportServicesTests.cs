using System.Text;
using GenericFileImportServices.Contracts;
using GenericFileImportServices.Models;
using GenericFileImportServices.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Moq;

namespace GenericFileImportServices.Tests.Services;

public class FileImportServicesTests
{
    private readonly Mock<IDatabaseServices<TestEntity, TestDbContext>> _dbMock = new();
    private readonly Mock<IFileReaderServices<TestDto>> _readerMock = new();
    private readonly Mock<ILogger<TestFileImportServices>> _loggerMock = new();
    private readonly TestFileImportServices _sut;

    private const string BasePath = "/imports";
    private const string Pattern = "data_*.csv";

    public FileImportServicesTests()
    {
        _sut = new TestFileImportServices(_dbMock.Object, _readerMock.Object, _loggerMock.Object);

        // Default: UpdateDatabaseAsync succeeds with 0 rows
        _dbMock.Setup(d => d.UpdateDatabaseAsync(
            It.IsAny<List<TestEntity>>(),
            It.IsAny<List<TestEntity>>(),
            It.IsAny<List<TestEntity>>(),
            It.IsAny<bool>()))
            .ReturnsAsync(0);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private FileResults<TestDto> MakeResult(string fileName, List<TestDto> data, List<string>? errors = null) =>
        new(fileName, data, errors ?? []);

    private void SetupReader(params FileResults<TestDto>[] results) =>
        _readerMock
            .Setup(r => r.ReadFromFile(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Encoding>(),
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<bool>()))
            .Returns([.. results]);

    private void SetupExistingEntities(params TestEntity[] entities) =>
        _dbMock.Setup(d => d.GetAllEntitiesAsync()).ReturnsAsync([.. entities]);

    // ── Argument validation ──────────────────────────────────────────────────

    [Fact]
    public async Task ProcessFileAsync_Throws_WhenBasePathIsEmpty()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.ProcessFileAsync("", Pattern, Encoding.UTF8, firstLineContainsEncoding: false));
    }

    [Fact]
    public async Task ProcessFileAsync_Throws_WhenPatternIsEmpty()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.ProcessFileAsync(BasePath, "", Encoding.UTF8, firstLineContainsEncoding: false));
    }

    // ── Happy path ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessFileAsync_ReturnsEmptyErrors_WhenFileImportsCleanly()
    {
        SetupExistingEntities();
        SetupReader(MakeResult("data.csv", [new TestDto { Name = "Alice" }]));

        var errors = await _sut.ProcessFileAsync(BasePath, Pattern, Encoding.UTF8, firstLineContainsEncoding: false);

        Assert.Empty(errors);
    }

    [Fact]
    public async Task ProcessFileAsync_CallsUpdateDatabase_WithReconciliationResults()
    {
        var existing = new TestEntity { Name = "Old" };
        SetupExistingEntities(existing);
        SetupReader(MakeResult("data.csv", [new TestDto { Name = "New" }]));

        await _sut.ProcessFileAsync(BasePath, Pattern, Encoding.UTF8, firstLineContainsEncoding: false);

        _dbMock.Verify(d => d.UpdateDatabaseAsync(
            It.Is<List<TestEntity>>(adds => adds.Any(e => e.Name == "New")),
            It.IsAny<List<TestEntity>>(),
            It.Is<List<TestEntity>>(deletes => deletes.Any(e => e.Name == "Old")),
            false),
            Times.Once);
    }

    [Fact]
    public async Task ProcessFileAsync_ArchivesFile_WhenNoErrors()
    {
        SetupExistingEntities();
        SetupReader(MakeResult("data.csv", [new TestDto { Name = "X" }]));

        await _sut.ProcessFileAsync(BasePath, Pattern, Encoding.UTF8, firstLineContainsEncoding: false, archiveIfSuccess: true);

        _readerMock.Verify(
            r => r.HandleFileSuccess(BasePath, "data.csv", It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessFileAsync_DoesNotArchive_WhenArchiveIfSuccessFalse()
    {
        SetupExistingEntities();
        SetupReader(MakeResult("data.csv", [new TestDto { Name = "X" }]));

        await _sut.ProcessFileAsync(BasePath, Pattern, Encoding.UTF8, firstLineContainsEncoding: false, archiveIfSuccess: false);

        _readerMock.Verify(
            r => r.HandleFileSuccess(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    // ── Error handling ───────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessFileAsync_CallsHandleFileError_WhenFileHasParseErrors()
    {
        SetupExistingEntities();
        SetupReader(MakeResult("data.csv", [], ["row 3: bad value"]));

        var errors = await _sut.ProcessFileAsync(BasePath, Pattern, Encoding.UTF8, firstLineContainsEncoding: false);

        _readerMock.Verify(
            r => r.HandleFileError(BasePath, "data.csv", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>()),
            Times.Once);
        Assert.Contains(errors, e => e.Contains("data.csv"));
    }

    [Fact]
    public async Task ProcessFileAsync_CallsHandleFileError_WhenDatabaseThrows()
    {
        SetupExistingEntities();
        SetupReader(MakeResult("data.csv", [new TestDto { Name = "X" }]));
        _dbMock.Setup(d => d.UpdateDatabaseAsync(
            It.IsAny<List<TestEntity>>(), It.IsAny<List<TestEntity>>(),
            It.IsAny<List<TestEntity>>(), It.IsAny<bool>()))
            .ThrowsAsync(new InvalidOperationException("constraint violation"));

        var errors = await _sut.ProcessFileAsync(BasePath, Pattern, Encoding.UTF8, firstLineContainsEncoding: false);

        _readerMock.Verify(
            r => r.HandleFileError(BasePath, "data.csv", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>()),
            Times.Once);
        Assert.Contains(errors, e => e.Contains("data.csv"));
    }

    // ── hardDelete forwarded ─────────────────────────────────────────────────

    [Fact]
    public async Task ProcessFileAsync_ForwardsHardDeleteTrue_ToUpdateDatabase()
    {
        SetupExistingEntities(new TestEntity { Name = "Gone" });
        SetupReader(MakeResult("data.csv", []));

        await _sut.ProcessFileAsync(BasePath, Pattern, Encoding.UTF8, hardDelete: true);

        _dbMock.Verify(d => d.UpdateDatabaseAsync(
            It.IsAny<List<TestEntity>>(), It.IsAny<List<TestEntity>>(),
            It.IsAny<List<TestEntity>>(), true),
            Times.Once);
    }

    // ── rowsToSkip / fixUnescapedQuotes forwarded ────────────────────────────

    [Fact]
    public async Task ProcessFileAsync_ForwardsRowsToSkipAndFixUnescapedQuotes_ToReader()
    {
        SetupExistingEntities();
        SetupReader(MakeResult("data.csv", []));

        await _sut.ProcessFileAsync(BasePath, Pattern, Encoding.UTF8, rowsToSkip: 2, fixUnescapedQuotes: true);

        _readerMock.Verify(
            r => r.ReadFromFile(BasePath, Pattern, Encoding.UTF8, ",", false, true, false, 2, true),
            Times.Once);
    }

    // ── Convenience overloads ────────────────────────────────────────────────

    [Fact]
    public async Task ProcessFileAsync_RowsToSkipOverload_ForwardsCorrectly()
    {
        SetupExistingEntities();
        SetupReader(MakeResult("data.csv", []));

        await _sut.ProcessFileAsync(BasePath, Pattern, rowsToSkip: 5);

        _readerMock.Verify(
            r => r.ReadFromFile(BasePath, Pattern, It.IsAny<Encoding>(), ",", false, true, false, 5, false),
            Times.Once);
    }

    [Fact]
    public async Task ProcessFileAsync_RowsToSkipAndFixQuotesOverload_ForwardsCorrectly()
    {
        SetupExistingEntities();
        SetupReader(MakeResult("data.csv", []));

        await _sut.ProcessFileAsync(BasePath, Pattern, rowsToSkip: 1, fixUnescapedQuotes: true);

        _readerMock.Verify(
            r => r.ReadFromFile(BasePath, Pattern, It.IsAny<Encoding>(), ",", false, true, false, 1, true),
            Times.Once);
    }

    // ── Multiple files: GetAllEntitiesAsync called once ──────────────────────

    [Fact]
    public async Task ProcessFileAsync_CallsGetAllEntitiesOnce_EvenWithMultipleFiles()
    {
        SetupExistingEntities();
        _readerMock
            .Setup(r => r.ReadFromFile(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Encoding>(),
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<bool>()))
            .Returns([
                MakeResult("file1.csv", [new TestDto { Name = "A" }]),
                MakeResult("file2.csv", [new TestDto { Name = "B" }])
            ]);

        await _sut.ProcessFileAsync(BasePath, Pattern, Encoding.UTF8, multipleFiles: true);

        _dbMock.Verify(d => d.GetAllEntitiesAsync(), Times.Once);
    }

    // ── Stream / blob overload ───────────────────────────────────────────────

    [Fact]
    public async Task ProcessFileAsync_Stream_ReturnsEmptyErrors_WhenImportCleanly()
    {
        var stream = new MemoryStream();
        SetupExistingEntities();
        _readerMock
            .Setup(r => r.ReadFromFile(stream, "upload.csv", Encoding.UTF8, false, ",", 0, false))
            .Returns([MakeResult("upload.csv", [new TestDto { Name = "X" }])]);

        var errors = await _sut.ProcessFileAsync(
            stream, "connstr", "container", "path/upload.csv", Encoding.UTF8);

        Assert.Empty(errors);
    }

    [Fact]
    public async Task ProcessFileAsync_Stream_ThrowsArgumentException_WhenConnectionStringEmpty()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.ProcessFileAsync(new MemoryStream(), "", "container", "file.csv", Encoding.UTF8));
    }

    [Fact]
    public async Task ProcessFileAsync_Stream_ThrowsArgumentException_WhenContainerNameEmpty()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.ProcessFileAsync(new MemoryStream(), "connstr", "", "file.csv", Encoding.UTF8));
    }

    [Fact]
    public async Task ProcessFileAsync_Stream_ThrowsArgumentException_WhenFilePathEmpty()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.ProcessFileAsync(new MemoryStream(), "connstr", "container", "", Encoding.UTF8));
    }

    [Fact]
    public async Task ProcessFileAsync_Stream_ArchivesBlob_WhenNoErrors()
    {
        var stream = new MemoryStream();
        SetupExistingEntities();
        _readerMock
            .Setup(r => r.ReadFromFile(stream, "upload.csv", Encoding.UTF8, false, ",", 0, false))
            .Returns([MakeResult("upload.csv", [new TestDto { Name = "X" }])]);
        _readerMock
            .Setup(r => r.HandleFileSuccessAsync(stream, "connstr", "container", "path/upload.csv", It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        await _sut.ProcessFileAsync(
            stream, "connstr", "container", "path/upload.csv", Encoding.UTF8, archiveIfSuccess: true);

        _readerMock.Verify(
            r => r.HandleFileSuccessAsync(stream, "connstr", "container", "path/upload.csv", It.IsAny<string>()),
            Times.Once);
    }
}
