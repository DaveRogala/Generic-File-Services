namespace GenericFileImportServices.Services;

/// <summary>
/// Default implementation of <see cref="IFileImportRecordServices{C}"/>.
/// Writes a <see cref="FileImportRecord"/> and its <see cref="FileImportEntityLink"/> rows
/// to the consumer's <typeparamref name="C"/> context via an injected
/// <see cref="IDbContextFactory{TContext}"/>.
/// </summary>
/// <typeparam name="C">EF Core <see cref="DbContext"/> type.</typeparam>
public class FileImportRecordServices<C>(
    IDbContextFactory<C> contextFactory,
    ILogger<FileImportRecordServices<C>> logger)
    : IFileImportRecordServices<C>
    where C : DbContext
{
    /// <inheritdoc/>
    public async Task RecordFileImportAsync(string importFileName, string archivedFileName, IEnumerable<int> entityIds)
    {
        try
        {
            await using C context = await contextFactory.CreateDbContextAsync();

            var record = new FileImportRecord
            {
                ImportFileName = importFileName,
                ArchivedFileName = archivedFileName,
                DateTimeAddedUtc = DateTime.UtcNow
            };
            context.Set<FileImportRecord>().Add(record);
            await context.SaveChangesAsync();

            var links = entityIds
                .Select(id => new FileImportEntityLink { FileImportRecordId = record.Id, EntityId = id })
                .ToList();

            if (links.Count > 0)
            {
                context.Set<FileImportEntityLink>().AddRange(links);
                await context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error recording file import metadata for {ImportFileName}", importFileName);
            throw;
        }
    }
}
