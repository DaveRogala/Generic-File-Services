using System.Linq.Expressions;

namespace GenericFileImportServices.Services;

public class DatabaseServices<T, C> : IDatabaseServices<T, C>
    where T : BaseObject
    where C : DbContext
{
    private readonly IGenericRepository<T, C> _repository;
    private readonly ILogger<DatabaseServices<T, C>> _logger;
    public DatabaseServices(IGenericRepository<T,C> repository,ILogger<DatabaseServices<T,C>> logger)
    {
        _logger = logger;
        _repository = repository;
    }
    public async Task<List<T>> FindEntitiesAsync(Expression<Func<T, bool>> predicate)
    {
        try
        {
            return [.. await _repository.FindAsync(predicate)];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            throw;
        }
    }
    public async Task<List<T>> GetAllEntitiesAsync()
    {
        try
        {
            return [.. await _repository.AllAsync()];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            throw;
        }
    }
    public async Task<int> UpdateDatabaseAsync(List<T> existingEntities, List<T> addEntities, List<T> updateEntities, List<T> deleteEntities, bool hardDelete= false)
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
            _logger.LogError(ex, ex.Message);
            throw;
        }
    }
}
