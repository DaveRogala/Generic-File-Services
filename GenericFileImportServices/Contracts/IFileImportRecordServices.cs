namespace GenericFileImportServices.Contracts;

/// <summary>
/// Records file import metadata and per-entity links after a successful file import.
/// </summary>
/// <remarks>
/// Register via
/// <c>services.AddFileImportMetadataServices&lt;TContext&gt;()</c>
/// and call <c>modelBuilder.AddFileImportMetadata()</c> in your <c>DbContext.OnModelCreating</c>.
/// </remarks>
/// <typeparam name="C">EF Core <see cref="DbContext"/> type.</typeparam>
public interface IFileImportRecordServices<C> where C : DbContext
{
    /// <summary>
    /// Persists a <see cref="FileImportRecord"/> for the given file and a
    /// <see cref="FileImportEntityLink"/> row for every entity that was touched.
    /// </summary>
    /// <param name="importFileName">Original file name as it appeared on disk or in blob storage.</param>
    /// <param name="archivedFileName">Name given to the file after it was moved to the archive location.</param>
    /// <param name="entityIds">IDs of the entities that were added or updated during the import.</param>
    Task RecordFileImportAsync(string importFileName, string archivedFileName, IEnumerable<int> entityIds);
}
