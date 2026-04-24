using GenericFileImportServices.Contracts;
using GenericFileImportServices.Models;
using GenericFileImportServices.Models.Database.Base;
using GenericFileImportServices.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GenericFileImportServices.Tests.TestHelpers;

public class TestEntity : BaseObject
{
    [SetsRequiredMembers]
    public TestEntity() { Name = null!; }

    public required string Name { get; set; }
}

public class TestDto
{
    public string Name { get; set; } = "";
}

public class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    public DbSet<TestEntity> TestEntities => Set<TestEntity>();
}

/// <summary>
/// Concrete implementation used in FileImportServices tests.
/// Add = DTOs with no matching entity name.
/// Delete = entities with no matching DTO name.
/// Update is not overridden — the default no-op is intentional.
/// </summary>
public class TestFileImportServices(
    IDatabaseServices<TestEntity, TestDbContext> databaseServices,
    IFileReaderServices<TestDto> fileReaderServices,
    ILogger<TestFileImportServices> logger)
    : FileImportServices<TestEntity, TestDto, TestDbContext>(databaseServices, fileReaderServices, logger)
{
    public override List<TestEntity> GetAddEntities(List<TestEntity> existingEntities, List<TestDto> dtos) =>
        dtos.Where(d => existingEntities.All(e => e.Name != d.Name))
            .Select(d => new TestEntity { Name = d.Name })
            .ToList();

    public override List<TestEntity> GetDeleteEntities(List<TestEntity> existingEntities, List<TestDto> dtos) =>
        existingEntities.Where(e => e.DateDeletedUtc is null && dtos.All(d => d.Name != e.Name))
                        .ToList();
}
