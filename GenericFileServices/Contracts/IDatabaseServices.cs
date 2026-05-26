using System.Linq.Expressions;

namespace GenericFileServices.Contracts;

/// <summary>
/// Provides database query and persistence operations for ETL import workflows.
/// </summary>
/// <typeparam name="T">Entity type. Must extend <see cref="GenericFileServices.Models.Database.Base.BaseObject"/>.</typeparam>
/// <typeparam name="C">EF Core <see cref="DbContext"/> type.</typeparam>
public interface IDatabaseServices<T, C>
    where T : BaseObject
    where C : DbContext
{
    /// <summary>Returns every non-deleted entity of type <typeparamref name="T"/>.</summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<List<T>> GetAllEntitiesAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns entities matching <paramref name="predicate"/>.</summary>
    /// <param name="predicate">Filter expression applied server-side.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<List<T>> FindEntitiesAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists adds, updates, and deletes in a single <c>SaveChanges</c> call.
    /// Timestamps (<c>DateAddedUtc</c>, <c>DateUpdatedUtc</c>, <c>DateDeletedUtc</c>) are set automatically.
    /// </summary>
    /// <param name="addEntities">Entities to insert.</param>
    /// <param name="updateEntities">Entities with field changes already applied.</param>
    /// <param name="deleteEntities">Entities to remove or soft-delete.</param>
    /// <param name="hardDelete">
    /// When <c>true</c>, entities in <paramref name="deleteEntities"/> are permanently removed.
    /// When <c>false</c> (default), <c>DateDeletedUtc</c> is set (soft delete).
    /// </param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The number of rows affected.</returns>
    Task<int> UpdateDatabaseAsync(List<T> addEntities, List<T> updateEntities, List<T> deleteEntities, bool hardDelete = false, CancellationToken cancellationToken = default);
}
