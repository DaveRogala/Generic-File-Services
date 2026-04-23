using System.Linq.Expressions;
using GenericFileImportServices.Services;
using GenericFileImportServices.Tests.TestHelpers;
using GenericRepositories.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;

namespace GenericFileImportServices.Tests.Services;

public class DatabaseServicesTests
{
    private readonly Mock<IGenericRepository<TestEntity, TestDbContext>> _repositoryMock = new();
    private readonly Mock<ILogger<DatabaseServices<TestEntity, TestDbContext>>> _loggerMock = new();
    private readonly DatabaseServices<TestEntity, TestDbContext> _sut;

    public DatabaseServicesTests()
    {
        _sut = new DatabaseServices<TestEntity, TestDbContext>(_repositoryMock.Object, _loggerMock.Object);
    }

    // ── GetAllEntitiesAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetAllEntitiesAsync_ReturnsAllEntities()
    {
        var entities = new List<TestEntity>
        {
            new() { Name = "Alpha" },
            new() { Name = "Beta" }
        };
        _repositoryMock.Setup(r => r.AllAsync()).ReturnsAsync(entities);

        var result = await _sut.GetAllEntitiesAsync();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, e => e.Name == "Alpha");
        Assert.Contains(result, e => e.Name == "Beta");
    }

    [Fact]
    public async Task GetAllEntitiesAsync_LogsAndRethrows_WhenRepositoryThrows()
    {
        _repositoryMock.Setup(r => r.AllAsync()).ThrowsAsync(new InvalidOperationException("db error"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.GetAllEntitiesAsync());
    }

    // ── FindEntitiesAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task FindEntitiesAsync_ReturnsMatchingEntities()
    {
        var entities = new List<TestEntity> { new() { Name = "Match" } };
        _repositoryMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<TestEntity, bool>>>()))
            .ReturnsAsync(entities);

        var result = await _sut.FindEntitiesAsync(e => e.Name == "Match");

        Assert.Single(result);
        Assert.Equal("Match", result[0].Name);
    }

    [Fact]
    public async Task FindEntitiesAsync_LogsAndRethrows_WhenRepositoryThrows()
    {
        _repositoryMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<TestEntity, bool>>>()))
            .ThrowsAsync(new InvalidOperationException("db error"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.FindEntitiesAsync(e => e.Name == "x"));
    }

    // ── UpdateDatabaseAsync — adds ───────────────────────────────────────────

    [Fact]
    public async Task UpdateDatabaseAsync_CallsAddAsync_ForEachAddEntity()
    {
        var toAdd = new List<TestEntity> { new() { Name = "New" } };
        _repositoryMock.Setup(r => r.AddAsync(It.IsAny<TestEntity>())).Returns(Task.CompletedTask);
        _repositoryMock.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        await _sut.UpdateDatabaseAsync(toAdd, [], []);

        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<TestEntity>()), Times.Once);
    }

    [Fact]
    public async Task UpdateDatabaseAsync_SetsDateAddedAndDateUpdated_OnAddedEntities()
    {
        var entity = new TestEntity { Name = "New" };
        _repositoryMock.Setup(r => r.AddAsync(It.IsAny<TestEntity>())).Returns(Task.CompletedTask);
        _repositoryMock.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        var before = DateTime.UtcNow;
        await _sut.UpdateDatabaseAsync([entity], [], []);
        var after = DateTime.UtcNow;

        Assert.InRange(entity.DateAddedUtc, before, after);
        Assert.InRange(entity.DateUpdatedUtc, before, after);
    }

    // ── UpdateDatabaseAsync — updates ────────────────────────────────────────

    [Fact]
    public async Task UpdateDatabaseAsync_CallsUpdate_ForEachUpdateEntity()
    {
        var toUpdate = new List<TestEntity> { new() { Name = "Existing" } };
        _repositoryMock.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        await _sut.UpdateDatabaseAsync([], toUpdate, []);

        _repositoryMock.Verify(r => r.Update(It.IsAny<TestEntity>()), Times.Once);
    }

    [Fact]
    public async Task UpdateDatabaseAsync_SetsDateUpdated_OnUpdatedEntities()
    {
        var entity = new TestEntity { Name = "Existing" };
        _repositoryMock.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        var before = DateTime.UtcNow;
        await _sut.UpdateDatabaseAsync([], [entity], []);
        var after = DateTime.UtcNow;

        Assert.InRange(entity.DateUpdatedUtc, before, after);
    }

    // ── UpdateDatabaseAsync — soft delete ────────────────────────────────────

    [Fact]
    public async Task UpdateDatabaseAsync_SoftDeletes_WhenHardDeleteFalse()
    {
        var entity = new TestEntity { Name = "Gone" };
        _repositoryMock.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        var before = DateTime.UtcNow;
        await _sut.UpdateDatabaseAsync([], [], [entity], hardDelete: false);
        var after = DateTime.UtcNow;

        Assert.NotNull(entity.DateDeletedUtc);
        Assert.InRange(entity.DateDeletedUtc!.Value, before, after);
        _repositoryMock.Verify(r => r.Update(entity), Times.Once);
        _repositoryMock.Verify(r => r.Delete(It.IsAny<TestEntity>()), Times.Never);
    }

    // ── UpdateDatabaseAsync — hard delete ────────────────────────────────────

    [Fact]
    public async Task UpdateDatabaseAsync_HardDeletes_WhenHardDeleteTrue()
    {
        var entity = new TestEntity { Name = "Gone" };
        _repositoryMock.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        await _sut.UpdateDatabaseAsync([], [], [entity], hardDelete: true);

        _repositoryMock.Verify(r => r.Delete(entity), Times.Once);
        _repositoryMock.Verify(r => r.Update(It.IsAny<TestEntity>()), Times.Never);
        Assert.Null(entity.DateDeletedUtc);
    }

    // ── UpdateDatabaseAsync — save count ─────────────────────────────────────

    [Fact]
    public async Task UpdateDatabaseAsync_ReturnsSaveChangesCount()
    {
        _repositoryMock.Setup(r => r.SaveChangesAsync()).ReturnsAsync(3);

        var result = await _sut.UpdateDatabaseAsync([], [], []);

        Assert.Equal(3, result);
    }

    [Fact]
    public async Task UpdateDatabaseAsync_LogsAndRethrows_WhenRepositoryThrows()
    {
        _repositoryMock.Setup(r => r.SaveChangesAsync())
            .ThrowsAsync(new InvalidOperationException("constraint violation"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.UpdateDatabaseAsync([], [], []));
    }
}
