using GenericFileImportServices.Repositories;
using GenericFileImportServices.Services;
using MagellanFileServices.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GenericFileImportServices;

/// <summary>
/// Extension methods for registering GenericFileImportServices with the .NET dependency injection container.
/// </summary>
public static class FileImportServiceCollectionExtensions
{
    /// <summary>
    /// Registers all GenericFileImportServices services and configures a new <see cref="IDbContextFactory{TContext}"/>
    /// using the supplied <paramref name="options"/> action.
    /// If an <see cref="IDbContextFactory{TContext}"/> for <typeparamref name="M"/> is already registered
    /// the existing registration is used and <paramref name="options"/> is ignored.
    /// </summary>
    /// <typeparam name="T">Entity type. Must extend <see cref="GenericFileImportServices.Models.Database.Base.BaseObject"/>.</typeparam>
    /// <typeparam name="U">DTO type that each parsed file row maps to.</typeparam>
    /// <typeparam name="M">EF Core <see cref="DbContext"/> type.</typeparam>
    /// <typeparam name="F">Concrete <see cref="IFileImportServices{T,U,M}"/> implementation to register.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="options">Action to configure the <see cref="DbContextOptionsBuilder"/>.</param>
    public static IServiceCollection AddFileImportServices<T, U, M, F>(this IServiceCollection services, Action<DbContextOptionsBuilder> options)
        where T : BaseObject
        where M : DbContext
        where F : class, IFileImportServices<T, U, M>
    {
        if (!services.Any(s => s.ServiceType == typeof(IDbContextFactory<M>)))
        {
            services.AddDbContextFactory<M>(options);
        }
        return services.AddFileImportServices<T, U, M, F>();
    }

    /// <summary>
    /// Registers all GenericFileImportServices services, assuming an <see cref="IDbContextFactory{TContext}"/>
    /// for <typeparamref name="M"/> has already been configured.
    /// </summary>
    /// <typeparam name="T">Entity type. Must extend <see cref="GenericFileImportServices.Models.Database.Base.BaseObject"/>.</typeparam>
    /// <typeparam name="U">DTO type that each parsed file row maps to.</typeparam>
    /// <typeparam name="M">EF Core <see cref="DbContext"/> type.</typeparam>
    /// <typeparam name="F">Concrete <see cref="IFileImportServices{T,U,M}"/> implementation to register.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    public static IServiceCollection AddFileImportServices<T, U, M, F>(this IServiceCollection services)
        where T : BaseObject
        where M : DbContext
        where F : class, IFileImportServices<T, U, M>
    {
        services.AddScoped<IGenericRepository<T, M>, Repository<T, M>>();
        services.AddScoped<IFileServices, FileServices>();
        services.AddScoped<IFileReaderServices<U>, FileReaderServices<U>>();
        services.AddScoped<IDatabaseServices<T, M>, DatabaseServices<T, M>>();
        services.AddScoped<IFileImportServices<T, U, M>, F>();

        return services;
    }
}
