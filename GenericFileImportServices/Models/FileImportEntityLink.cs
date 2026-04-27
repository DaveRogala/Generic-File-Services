namespace GenericFileImportServices.Models;

/// <summary>
/// Junction row that links a <see cref="FileImportRecord"/> to an entity that was
/// added or updated during that import.
/// <para>
/// <see cref="EntityId"/> is a logical foreign key to the consumer's entity table.
/// No EF Core navigation is declared for it because the entity type is generic.
/// Consumers who want a database-enforced constraint can add the FK manually in
/// <c>OnModelCreating</c>.
/// </para>
/// </summary>
public class FileImportEntityLink
{
    /// <summary>Foreign key to the parent <see cref="FileImportRecord"/>.</summary>
    public int FileImportRecordId { get; set; }

    /// <summary>Navigation to the parent <see cref="FileImportRecord"/>.</summary>
    public FileImportRecord FileImportRecord { get; set; } = null!;

    /// <summary>Primary key of the entity that was added or updated during the import.</summary>
    public int EntityId { get; set; }
}
