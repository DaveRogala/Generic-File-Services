namespace GenericFileImportServices.Models;

/// <summary>
/// Stores metadata about a completed file import operation.
/// Add <see cref="DbSet{TEntity}"/> entries for this type and for
/// <see cref="FileImportEntityLink"/> to your <c>DbContext</c>, then call
/// <c>modelBuilder.AddFileImportMetadata()</c> in <c>OnModelCreating</c>.
/// </summary>
public class FileImportRecord
{
    /// <summary>Surrogate primary key.</summary>
    public int Id { get; set; }

    /// <summary>Original name of the imported file as it appeared on disk or in blob storage.</summary>
    public required string ImportFileName { get; set; }

    /// <summary>Name given to the file after it was moved to the archive location.</summary>
    public required string ArchivedFileName { get; set; }

    /// <summary>UTC timestamp at which the import was recorded.</summary>
    public DateTime DateTimeAddedUtc { get; set; } = DateTime.UtcNow;
}
