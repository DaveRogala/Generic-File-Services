using System.Linq.Expressions;

namespace GenericFileImportServices.Services;

/// <summary>
/// Default implementation of <see cref="IDatabaseServices{T,C}"/>.
/// Delegates persistence to <see cref="IGenericRepository{T,C,TKey}"/> and stamps UTC timestamps
/// on all mutations via the <see cref="BaseObject"/> audit fields.
/// </summary>
public class DatabaseServices<T, C> : IDatabaseServices<T, C>
    where T : BaseObject
    where C : DbContext
{
    private readonly IGenericRepository<T, C, int> _repository;
    private readonly ILogger<DatabaseServices<T, C>> _logger;

    /// <summary>Initializes a new instance with the required collaborators.</summary>
    public DatabaseServices(IGenericRepository<T, C, int> repository, ILogger<DatabaseServices<T, C>> logger)
    {
        _logger = logger;
        _repository = repository;
    }

    /// <inheritdoc/>
    public async Task<List<T>> FindEntitiesAsync(Expression<Func<T, bool>> predicate)
    {
        try
        {
            return [.. await _repository.FindAsync(predicate)];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding entities");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<List<T>> GetAllEntitiesAsync()
    {
        try
        {
            return [.. await _repository.AllAsync()];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all entities");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<int> UpdateDatabaseAsync(List<T> addEntities, List<T> updateEntities, List<T> deleteEntities, bool hardDelete = false)
    {
        try
        {
            DateTime utcNow = DateTime.UtcNow;

            foreach (T entity in updateEntities)
            {
                entity.DateUpdatedUtc = utcNow;
                _repository.Update(entity);
            }
            foreach (T entity in addEntities)
            {
                entity.DateUpdatedUtc = utcNow;
                entity.DateAddedUtc = utcNow;
                await _repository.AddAsync(entity);
            }
            foreach (var entity in deleteEntities)
            {
                if (hardDelete)
                {
                    _repository.Delete(entity);
                }
                else
                {
                    entity.DateDeletedUtc = utcNow;
                    entity.DateUpdatedUtc = utcNow;
                    _repository.Update(entity);
                }
            }
            return await _repository.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating database");
            throw;
        }
    }
}
