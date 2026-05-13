using GenericFileServices.Repositories;
using GenericFileServices.Services;
using MagellanFileServices.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GenericFileServices;

/// <summary>
/// Extension methods for registering GenericFileServices import and export services
/// with the .NET dependency injection container.
/// </summary>
public static class FileServiceCollectionExtensions
{
    /// <summary>
    /// Registers all GenericFileServices services and configures a new <see cref="IDbContextFactory{TContext}"/>
    /// using the supplied <paramref name="options"/> action.
    /// If an <see cref="IDbContextFactory{TContext}"/> for <typeparamref name="M"/> is already registered
    /// the existing registration is used and <paramref name="options"/> is ignored.
    /// </summary>
    /// <typeparam name="T">Entity type. Must extend <see cref="GenericFileServices.Models.Database.Base.BaseObject"/>.</typeparam>
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
    /// Registers all GenericFileServices services, assuming an <see cref="IDbContextFactory{TContext}"/>
    /// for <typeparamref name="M"/> has already been configured.
    /// </summary>
    /// <typeparam name="T">Entity type. Must extend <see cref="GenericFileServices.Models.Database.Base.BaseObject"/>.</typeparam>
    /// <typeparam name="U">DTO type that each parsed file row maps to.</typeparam>
    /// <typeparam name="M">EF Core <see cref="DbContext"/> type.</typeparam>
    /// <typeparam name="F">Concrete <see cref="IFileImportServices{T,U,M}"/> implementation to register.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    public static IServiceCollection AddFileImportServices<T, U, M, F>(this IServiceCollection services)
        where T : BaseObject
        where M : DbContext
        where F : class, IFileImportServices<T, U, M>
    {
        services.AddScoped<IGenericRepository<T, M, int>, Repository<T, M>>();
        services.AddScoped<IFileServices, FileServices>();
        services.AddSingleton<IBlobClientFactory, BlobClientFactory>();
        services.AddScoped<IFileReaderServices<U>, FileReaderServices<U>>();
        services.AddScoped<IDatabaseServices<T, M>, DatabaseServices<T, M>>();
        services.AddScoped<IFileImportServices<T, U, M>, F>();

        return services;
    }

    /// <summary>
    /// Registers export services and configures a new <see cref="IDbContextFactory{TContext}"/>
    /// using the supplied <paramref name="options"/> action.
    /// If an <see cref="IDbContextFactory{TContext}"/> for <typeparamref name="M"/> is already registered
    /// the existing registration is used and <paramref name="options"/> is ignored.
    /// </summary>
    /// <typeparam name="T">DTO or record type produced by the data provider. Must be a reference type.</typeparam>
    /// <typeparam name="M">EF Core <see cref="DbContext"/> type.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="options">Action to configure the <see cref="DbContextOptionsBuilder"/>.</param>
    public static IServiceCollection AddFileExportServices<T, M>(this IServiceCollection services, Action<DbContextOptionsBuilder> options)
        where T : class
        where M : DbContext
    {
        if (!services.Any(s => s.ServiceType == typeof(IDbContextFactory<M>)))
            services.AddDbContextFactory<M>(options);

        return services.AddFileExportServices<T, M>();
    }

    /// <summary>
    /// Registers export services, assuming an <see cref="IDbContextFactory{TContext}"/>
    /// for <typeparamref name="M"/> has already been configured.
    /// </summary>
    /// <typeparam name="T">DTO or record type produced by the data provider. Must be a reference type.</typeparam>
    /// <typeparam name="M">EF Core <see cref="DbContext"/> type.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    public static IServiceCollection AddFileExportServices<T, M>(this IServiceCollection services)
        where T : class
        where M : DbContext
    {
        services.AddSingleton<IBlobClientFactory, BlobClientFactory>();
        services.AddScoped<IFileWriterServices, FileWriterServices>();
        services.AddScoped<IFileExportServices<T, M>, FileExportServices<T, M>>();
        return services;
    }
}
