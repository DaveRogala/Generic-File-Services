using GenericFileImportServices.Repositories;
using GenericFileImportServices.Services;
using MagellanFileServices.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GenericFileImportServices
{
    public static class FileImportServiceCollectionExtensions
    {
        public static IServiceCollection AddFileImportServices<T,U,M,F>(this IServiceCollection services, Action<DbContextOptionsBuilder> options)
            where T : BaseObject
            where M : DbContext
            where F : class, IFileImportServices<T, U, M>
        {
            if(!services.Any(s => s.ServiceType == typeof(IDbContextFactory<M>)))
            {
                services.AddDbContextFactory<M>(options);
            }            
            return services.AddFileImportServices<T, U, M, F>();
        }
        public static IServiceCollection AddFileImportServices<T, U, M, F>(this IServiceCollection services)
            where T : BaseObject
            where M : DbContext
            where F : FileImportServices<T, U, M>
        {           
            services.AddScoped<IGenericRepository<T, M>, Repository<T, M>>();
            services.AddScoped<IFileServices, FileServices>();
            services.AddScoped<IFileReaderServices<U>, FileReaderServices<U>>();            
            services.AddScoped<IDatabaseServices<T,M>, DatabaseServices<T,M>>();
            services.AddScoped<IFileImportServices<T, U, M>, F>();

            return services;
        }
    }
}
