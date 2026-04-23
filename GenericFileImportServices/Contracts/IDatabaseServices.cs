using System.Linq.Expressions;

namespace GenericFileImportServices.Contracts;
public interface IDatabaseServices<T,C>
    where T : BaseObject
    where C : DbContext
{
    Task<List<T>> GetAllEntitiesAsync();
    Task<List<T>> FindEntitiesAsync(Expression<Func<T, bool>> predicate);
    Task<int> UpdateDatabaseAsync(List<T> existingEntities, List<T> addEntities, List<T> updateEntities, List<T> deleteEntities, bool hardDelete = false);
}
