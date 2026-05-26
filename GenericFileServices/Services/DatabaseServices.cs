using System.Linq.Expressions;

namespace GenericFileServices.Services;

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
    public async Task<List<T>> FindEntitiesAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        try
        {
            return [.. await _repository.FindAsync(predicate, cancellationToken: cancellationToken)];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding entities");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<List<T>> GetAllEntitiesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return [.. await _repository.AllAsync(cancellationToken: cancellationToken)];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all entities");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<int> UpdateDatabaseAsync(List<T> addEntities, List<T> updateEntities, List<T> deleteEntities, bool hardDelete = false, CancellationToken cancellationToken = default)
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
                await _repository.AddAsync(entity, cancellationToken);
            }
            if (hardDelete)
            {
                foreach (var entity in deleteEntities)
                    _repository.Delete(entity);
            }
            else
            {
                foreach (var entity in deleteEntities)
                {
                    entity.DateDeletedUtc = utcNow;
                    entity.DateUpdatedUtc = utcNow;
                    _repository.Update(entity);
                }
            }
            return await _repository.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating database");
            throw;
        }
    }
}
