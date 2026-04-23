using GenericFileImportServices.Models.Database.Base;
using GenericFileImportServices.Tests.TestHelpers;

namespace GenericFileImportServices.Tests.Models;

public class BaseObjectTests
{
    [Fact]
    public void DefaultConstructor_SetsDateAddedAndDateUpdated_ToUtcNow()
    {
        var before = DateTime.UtcNow;
        var entity = new TestEntity { Name = "test" };
        var after = DateTime.UtcNow;

        Assert.InRange(entity.DateAddedUtc, before, after);
        Assert.InRange(entity.DateUpdatedUtc, before, after);
    }

    [Fact]
    public void DefaultConstructor_LeavesDateDeletedUtc_Null()
    {
        var entity = new TestEntity { Name = "test" };

        Assert.Null(entity.DateDeletedUtc);
    }

    [Fact]
    public void ParameterisedConstructor_SetsTimestampsToProvidedValues()
    {
        var added = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var updated = new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc);

        var entity = new BaseObjectSubclass(added, updated);

        Assert.Equal(added, entity.DateAddedUtc);
        Assert.Equal(updated, entity.DateUpdatedUtc);
    }

    [Fact]
    public void ParameterisedConstructor_LeavesDateDeletedUtc_Null()
    {
        var entity = new BaseObjectSubclass(DateTime.UtcNow, DateTime.UtcNow);

        Assert.Null(entity.DateDeletedUtc);
    }

    [Fact]
    public void DateDeletedUtc_CanBeSet()
    {
        var deleted = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var entity = new TestEntity { Name = "test" };

        entity.DateDeletedUtc = deleted;

        Assert.Equal(deleted, entity.DateDeletedUtc);
    }

    // Minimal concrete subclass to exercise the protected parameterised constructor.
    private sealed class BaseObjectSubclass : BaseObject
    {
        public BaseObjectSubclass(DateTime added, DateTime updated) : base(added, updated) { }
    }
}
