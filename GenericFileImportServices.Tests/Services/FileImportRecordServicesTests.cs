using GenericFileImportServices.Models;
using GenericFileImportServices.Services;
using GenericFileImportServices.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace GenericFileImportServices.Tests.Services;

public class FileImportRecordServicesTests : IDisposable
{
    private readonly MetadataTestDbContext _context;
    private readonly Mock<IDbContextFactory<MetadataTestDbContext>> _factoryMock = new();
    private readonly Mock<ILogger<FileImportRecordServices<MetadataTestDbContext>>> _loggerMock = new();
    private readonly FileImportRecordServices<MetadataTestDbContext> _sut;

    public FileImportRecordServicesTests()
    {
        var options = new DbContextOptionsBuilder<MetadataTestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new MetadataTestDbContext(options);
        _factoryMock
            .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_context);
        _sut = new FileImportRecordServices<MetadataTestDbContext>(_factoryMock.Object, _loggerMock.Object);
    }

    public void Dispose() => _context.Dispose();

    // ── FileImportRecord creation ─────────────────────────────────────────────

    [Fact]
    public async Task RecordFileImportAsync_CreatesFileImportRecord_WithCorrectFields()
    {
        var before = DateTime.UtcNow;

        await _sut.RecordFileImportAsync("orders.csv", "orders_20260427120000000.csv", [1, 2]);

        var record = await _context.Set<FileImportRecord>().SingleAsync();
        Assert.Equal("orders.csv", record.ImportFileName);
        Assert.Equal("orders_20260427120000000.csv", record.ArchivedFileName);
        Assert.True(record.DateTimeAddedUtc >= before);
    }

    [Fact]
    public async Task RecordFileImportAsync_AssignsPositiveId_ToRecord()
    {
        await _sut.RecordFileImportAsync("orders.csv", "orders_archived.csv", []);

        var record = await _context.Set<FileImportRecord>().SingleAsync();
        Assert.True(record.Id > 0);
    }

    // ── FileImportEntityLink creation ─────────────────────────────────────────

    [Fact]
    public async Task RecordFileImportAsync_CreatesEntityLinks_ForEachEntityId()
    {
        await _sut.RecordFileImportAsync("orders.csv", "orders_archived.csv", [10, 20, 30]);

        var links = await _context.Set<FileImportEntityLink>().ToListAsync();
        Assert.Equal(3, links.Count);
        Assert.Contains(links, l => l.EntityId == 10);
        Assert.Contains(links, l => l.EntityId == 20);
        Assert.Contains(links, l => l.EntityId == 30);
    }

    [Fact]
    public async Task RecordFileImportAsync_LinksAllPointToSameRecord()
    {
        await _sut.RecordFileImportAsync("orders.csv", "orders_archived.csv", [5, 6]);

        var record = await _context.Set<FileImportRecord>().SingleAsync();
        var links = await _context.Set<FileImportEntityLink>().ToListAsync();
        Assert.All(links, l => Assert.Equal(record.Id, l.FileImportRecordId));
    }

    // ── No entity IDs ─────────────────────────────────────────────────────────

    [Fact]
    public async Task RecordFileImportAsync_CreatesOnlyRecord_WhenNoEntityIds()
    {
        await _sut.RecordFileImportAsync("orders.csv", "orders_archived.csv", []);

        var recordCount = await _context.Set<FileImportRecord>().CountAsync();
        var linkCount = await _context.Set<FileImportEntityLink>().CountAsync();
        Assert.Equal(1, recordCount);
        Assert.Equal(0, linkCount);
    }
}
