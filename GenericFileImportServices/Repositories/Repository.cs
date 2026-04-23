using GenericRepositories;

namespace GenericFileImportServices.Repositories;

/// <summary>
/// Thin adapter that binds <c>GenericRepository</c> to the <c>BaseObject</c> constraint
/// required by this library's DI registration.
/// </summary>
public class Repository<T, M>(IDbContextFactory<M> contextFactory, ILogger<Repository<T, M>> logger)
    : GenericRepository<T, M>(contextFactory, logger)
    where T : BaseObject where M : DbContext;
