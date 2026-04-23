namespace GenericFileImportServices.Repositories;

/// <summary>
/// Thin adapter that binds <c>GenericRepository</c> to the <c>BaseObject</c> constraint
/// required by this library's DI registration.
/// </summary>
public class Repository<T, M>(IDbContextFactory<M> contextFactory, ILogger<GenericRepository<T, M, int>> logger)
    : GenericRepository<T, M, int>(contextFactory.CreateDbContext(), logger)
    where T : BaseObject where M : DbContext;
