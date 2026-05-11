namespace GenericFileServices.Contracts;

/// <summary>
/// Orchestrates a complete database-to-file export: data retrieval, row formatting,
/// file writing, and error reporting.
/// </summary>
/// <remarks>
/// The consuming application is responsible for the data source definition and
/// row serialisation. The data source may be a table, view, stored procedure, or
/// table-valued function — the library does not constrain the query shape.
/// </remarks>
/// <typeparam name="T">
/// The type returned by the data provider. May be an entity, a DTO, or any
/// projection type; it is not required to extend <see cref="GenericFileServices.Models.Database.Base.BaseObject"/>.
/// </typeparam>
/// <typeparam name="C">EF Core <see cref="DbContext"/> type used by the consuming application.</typeparam>
public interface IFileExportServices<T, C>
    where T : class
    where C : DbContext
{
    /// <summary>
    /// Fetches data via <paramref name="dataProvider"/>, maps it to file rows using
    /// <paramref name="rowMapper"/>, and writes the result to a local or network file path.
    /// </summary>
    /// <param name="basePath">Target directory (local or UNC path). The directory must exist.</param>
    /// <param name="fileName">Output file name, e.g. <c>"orders_20260101.csv"</c>.</param>
    /// <param name="dataProvider">
    /// Async delegate that retrieves the export data set. Supports any EF Core
    /// query form: <c>DbSet</c> queries, <c>FromSqlRaw</c> (stored procedures),
    /// <c>SqlQuery</c> (table-valued functions), or arbitrary projections.
    /// </param>
    /// <param name="rowMapper">
    /// Maps a single <typeparamref name="T"/> instance to an ordered sequence of
    /// field values. Field count must match <paramref name="headers"/>.
    /// </param>
    /// <param name="headers">
    /// Column header names written as the first row. Pass an empty sequence to omit the header.
    /// </param>
    /// <param name="encoding">Character encoding. Defaults to UTF-8 without BOM when not specified.</param>
    /// <param name="delimiter">Column delimiter. Defaults to <c>","</c>.</param>
    /// <param name="archiveExistingFile">
    /// When <c>true</c> and a file named <paramref name="fileName"/> already exists in
    /// <paramref name="basePath"/>, the existing file is timestamped and moved to the archive
    /// directory before the new file is written.
    /// </param>
    /// <param name="archivePath">
    /// Archive directory used when <paramref name="archiveExistingFile"/> is <c>true</c>.
    /// Absolute paths are used as-is; relative paths are resolved relative to <paramref name="basePath"/>.
    /// When <c>null</c>, defaults to an <c>archive</c> sub-folder of <paramref name="basePath"/>.
    /// The directory is created automatically if it does not exist.
    /// </param>
    /// <returns>A list of error messages. An empty list indicates a successful export.</returns>
    Task<List<string>> ExportToFileAsync(
        string basePath,
        string fileName,
        Func<Task<List<T>>> dataProvider,
        Func<T, IEnumerable<string>> rowMapper,
        IEnumerable<string> headers,
        Encoding encoding,
        string delimiter = ",",
        bool archiveExistingFile = false,
        string? archivePath = null);

    /// <summary>
    /// Fetches data via <paramref name="dataProvider"/>, maps it to file rows using
    /// <paramref name="rowMapper"/>, and uploads the result to an Azure Blob Storage container.
    /// An existing blob at the same path is overwritten.
    /// </summary>
    /// <param name="blobConnectionString">Azure Storage connection string.</param>
    /// <param name="containerName">Target blob container name.</param>
    /// <param name="blobPath">
    /// Full blob path for the output file, e.g. <c>"exports/orders_20260101.csv"</c>.
    /// </param>
    /// <param name="dataProvider">
    /// Async delegate that retrieves the export data set. Supports any EF Core
    /// query form: <c>DbSet</c> queries, <c>FromSqlRaw</c> (stored procedures),
    /// <c>SqlQuery</c> (table-valued functions), or arbitrary projections.
    /// </param>
    /// <param name="rowMapper">
    /// Maps a single <typeparamref name="T"/> instance to an ordered sequence of
    /// field values. Field count must match <paramref name="headers"/>.
    /// </param>
    /// <param name="headers">
    /// Column header names written as the first row. Pass an empty sequence to omit the header.
    /// </param>
    /// <param name="encoding">Character encoding. Defaults to UTF-8 without BOM when not specified.</param>
    /// <param name="delimiter">Column delimiter. Defaults to <c>","</c>.</param>
    /// <returns>A list of error messages. An empty list indicates a successful export.</returns>
    Task<List<string>> ExportToBlobAsync(
        string blobConnectionString,
        string containerName,
        string blobPath,
        Func<Task<List<T>>> dataProvider,
        Func<T, IEnumerable<string>> rowMapper,
        IEnumerable<string> headers,
        Encoding encoding,
        string delimiter = ",");
}
