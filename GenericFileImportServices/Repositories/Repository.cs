using GenericRepositories;

namespace GenericFileImportServices.Repositories;

public class Repository<T,M>(IDbContextFactory<M> contextFactory, ILogger<Repository<T,M>> logger)  
    : GenericRepository<T, M>(contextFactory, logger)
    where T: BaseObject where M: DbContext;
