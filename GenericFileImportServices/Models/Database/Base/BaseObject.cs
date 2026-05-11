namespace GenericFileImportServices.Models.Database.Base;

/// <summary>
/// Base class for all database entities managed by GenericFileImportServices.
/// Provides a surrogate integer key, UTC audit timestamps, and soft-delete support.
/// <c>DateDeletedUtc</c> is indexed to keep soft-delete filtered queries efficient.
/// </summary>
[Index(nameof(DateDeletedUtc))]
public class BaseObject
{
    /// <summary>Initialises timestamps to <see cref="DateTime.UtcNow"/>.</summary>
    [SetsRequiredMembers]
    public BaseObject()
    {
        DateAddedUtc = DateTime.UtcNow;
        DateUpdatedUtc = DateTime.UtcNow;
    }

    /// <summary>Initialises timestamps to the supplied values.</summary>
    [SetsRequiredMembers]
    public BaseObject(DateTime dateAddedUtc, DateTime dateUpdatedUtc)
    {
        DateAddedUtc = dateAddedUtc;
        DateUpdatedUtc = dateUpdatedUtc;
    }

    /// <summary>Surrogate primary key.</summary>
    public int Id { get; set; }

    /// <summary>UTC timestamp set when the row was first inserted.</summary>
    public required DateTime DateAddedUtc { get; set; }

    /// <summary>UTC timestamp updated on every write.</summary>
    public required DateTime DateUpdatedUtc { get; set; }

    /// <summary>
    /// UTC timestamp set when the row is soft-deleted. <c>null</c> means the row is active.
    /// Hard-deletes remove the row entirely rather than setting this field.
    /// </summary>
    public DateTime? DateDeletedUtc { get; set; }
}
