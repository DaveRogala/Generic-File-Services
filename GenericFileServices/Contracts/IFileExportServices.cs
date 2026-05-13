namespace GenericFileServices.Contracts;

/// <summary>
/// Orchestrates a complete database-to-file export: data retrieval, CSV serialisation
/// via CsvHelper, file writing, and error reporting.
/// </summary>
/// <remarks>
/// The consuming application is responsible for the data source definition.
/// The data source may be a table, view, stored procedure, or table-valued function —
/// the library does not constrain the query shape.
/// Column headers and field formatting are derived from <typeparamref name="T"/>
/// via CsvHelper's auto-map or a registered <c>ClassMap&lt;T&gt;</c>.
/// </remarks>
/// <typeparam name="T">
/// DTO or record type returned by the data provider and serialised by CsvHelper.
/// Not required to extend <see cref="GenericFileServices.Models.Database.Base.BaseObject"/>.
/// </typeparam>
/// <typeparam name="C">EF Core <see cref="DbContext"/> type used by the consuming application.</typeparam>
public interface IFileExportServices<T, C>
    where T : class
    where C : DbContext
{
    /// <summary>
    /// Fetches data via <paramref name="dataProvider"/>, serialises it with CsvHelper,
    /// and writes the result to a local or network file path.
    /// </summary>
    /// <param name="basePath">Target directory (local or UNC path). The directory must exist.</param>
    /// <param name="fileName">Output file name, e.g. <c>"orders_20260101.csv"</c>.</param>
    /// <param name="dataProvider">
    /// Async delegate that retrieves the export data set. Supports any EF Core
    /// query form: <c>DbSet</c> queries, <c>FromSqlRaw</c> (stored procedures),
    /// <c>SqlQuery</c> (table-valued functions), or arbitrary projections.
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
    /// <param name="metadataHeader">
    /// Optional metadata lines written before the encoding and column header rows.
    /// Entries are written in ascending key order; gaps in the key sequence are ignored.
    /// </param>
    /// <param name="writeHeader">When <c>true</c> (the default), writes a column header row. Set to <c>false</c> to produce a data-only file.</param>
    /// <param name="writeEncodingHeader">When <c>true</c>, writes the encoding as the first line of the file.</param>
    /// <param name="encodingHeaderOverride">
    /// Overrides the encoding string written when <paramref name="writeEncodingHeader"/> is <c>true</c>.
    /// When <c>null</c>, defaults to <see cref="Encoding.WebName"/> (e.g. <c>"utf-8"</c>).
    /// </param>
    /// <returns>A list of error messages. An empty list indicates a successful export.</returns>
    Task<List<string>> ExportToFileAsync(
        string basePath,
        string fileName,
        Func<Task<List<T>>> dataProvider,
        Encoding encoding,
        string delimiter = ",",
        bool archiveExistingFile = false,
        string? archivePath = null,
        IReadOnlyDictionary<int, string>? metadataHeader = null,
        bool writeHeader = true,
        bool writeEncodingHeader = false,
        string? encodingHeaderOverride = null);

    /// <summary>
    /// Fetches data via <paramref name="dataProvider"/>, serialises it with CsvHelper
    /// using a caller-supplied <see cref="CsvConfiguration"/>, and writes the result
    /// to a local or network file path.
    /// Use this overload when you need full control over CsvHelper behaviour —
    /// for example, to force quoting on specific columns via <see cref="CsvConfiguration.ShouldQuote"/>,
    /// register a <c>ClassMap&lt;T&gt;</c>, or configure any other CsvHelper setting.
    /// </summary>
    /// <param name="basePath">Target directory (local or UNC path). The directory must exist.</param>
    /// <param name="fileName">Output file name, e.g. <c>"orders_20260101.csv"</c>.</param>
    /// <param name="dataProvider">
    /// Async delegate that retrieves the export data set.
    /// </param>
    /// <param name="encoding">Character encoding.</param>
    /// <param name="csvConfiguration">
    /// CsvHelper configuration passed directly to <see cref="CsvWriter"/>.
    /// The caller is responsible for setting all relevant options, including
    /// <see cref="CsvConfiguration.Delimiter"/> and <see cref="CsvConfiguration.HasHeaderRecord"/>.
    /// </param>
    /// <param name="archiveExistingFile">
    /// When <c>true</c> and a file named <paramref name="fileName"/> already exists in
    /// <paramref name="basePath"/>, the existing file is timestamped and moved to the archive
    /// directory before the new file is written.
    /// </param>
    /// <param name="archivePath">
    /// Archive directory used when <paramref name="archiveExistingFile"/> is <c>true</c>.
    /// Absolute paths are used as-is; relative paths are resolved relative to <paramref name="basePath"/>.
    /// When <c>null</c>, defaults to an <c>archive</c> sub-folder of <paramref name="basePath"/>.
    /// </param>
    /// <param name="metadataHeader">
    /// Optional metadata lines written before the encoding and column header rows.
    /// Entries are written in ascending key order; gaps in the key sequence are ignored.
    /// </param>
    /// <param name="writeEncodingHeader">When <c>true</c>, writes the encoding as the first line of the file.</param>
    /// <param name="encodingHeaderOverride">
    /// Overrides the encoding string written when <paramref name="writeEncodingHeader"/> is <c>true</c>.
    /// When <c>null</c>, defaults to <see cref="Encoding.WebName"/> (e.g. <c>"utf-8"</c>).
    /// </param>
    /// <returns>A list of error messages. An empty list indicates a successful export.</returns>
    Task<List<string>> ExportToFileAsync(
        string basePath,
        string fileName,
        Func<Task<List<T>>> dataProvider,
        Encoding encoding,
        CsvConfiguration csvConfiguration,
        bool archiveExistingFile = false,
        string? archivePath = null,
        IReadOnlyDictionary<int, string>? metadataHeader = null,
        bool writeEncodingHeader = false,
        string? encodingHeaderOverride = null);

    /// <summary>
    /// Fetches data via <paramref name="dataProvider"/>, serialises it with CsvHelper,
    /// and uploads the result to an Azure Blob Storage container.
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
    /// <param name="encoding">Character encoding. Defaults to UTF-8 without BOM when not specified.</param>
    /// <param name="delimiter">Column delimiter. Defaults to <c>","</c>.</param>
    /// <param name="archiveExistingBlob">
    /// When <c>true</c> and a blob at <paramref name="blobPath"/> already exists, it is
    /// timestamped and moved to the archive path before the new blob is uploaded.
    /// </param>
    /// <param name="archivePath">
    /// Blob path prefix for the archived blob, used when <paramref name="archiveExistingBlob"/> is <c>true</c>.
    /// When <c>null</c>, defaults to an <c>archive</c> folder inside the blob's current directory.
    /// </param>
    /// <param name="metadataHeader">
    /// Optional metadata lines written before the encoding and column header rows.
    /// Entries are written in ascending key order; gaps in the key sequence are ignored.
    /// </param>
    /// <param name="writeHeader">When <c>true</c> (the default), writes a column header row. Set to <c>false</c> to produce a data-only blob.</param>
    /// <param name="writeEncodingHeader">When <c>true</c>, writes the encoding as the first line of the blob content.</param>
    /// <param name="encodingHeaderOverride">
    /// Overrides the encoding string written when <paramref name="writeEncodingHeader"/> is <c>true</c>.
    /// When <c>null</c>, defaults to <see cref="Encoding.WebName"/> (e.g. <c>"utf-8"</c>).
    /// </param>
    /// <returns>A list of error messages. An empty list indicates a successful export.</returns>
    Task<List<string>> ExportToBlobAsync(
        string blobConnectionString,
        string containerName,
        string blobPath,
        Func<Task<List<T>>> dataProvider,
        Encoding encoding,
        string delimiter = ",",
        bool archiveExistingBlob = false,
        string? archivePath = null,
        IReadOnlyDictionary<int, string>? metadataHeader = null,
        bool writeHeader = true,
        bool writeEncodingHeader = false,
        string? encodingHeaderOverride = null);

    /// <summary>
    /// Fetches data via <paramref name="dataProvider"/>, serialises it with CsvHelper
    /// using a caller-supplied <see cref="CsvConfiguration"/>, and uploads the result
    /// to an Azure Blob Storage container.
    /// Use this overload when you need full control over CsvHelper behaviour —
    /// for example, to force quoting on specific columns via <see cref="CsvConfiguration.ShouldQuote"/>,
    /// register a <c>ClassMap&lt;T&gt;</c>, or configure any other CsvHelper setting.
    /// An existing blob at the same path is overwritten.
    /// </summary>
    /// <param name="blobConnectionString">Azure Storage connection string.</param>
    /// <param name="containerName">Target blob container name.</param>
    /// <param name="blobPath">Full blob path for the output file, e.g. <c>"exports/orders_20260101.csv"</c>.</param>
    /// <param name="dataProvider">Async delegate that retrieves the export data set.</param>
    /// <param name="encoding">Character encoding.</param>
    /// <param name="csvConfiguration">
    /// CsvHelper configuration passed directly to <see cref="CsvWriter"/>.
    /// The caller is responsible for setting all relevant options, including
    /// <see cref="CsvConfiguration.Delimiter"/> and <see cref="CsvConfiguration.HasHeaderRecord"/>.
    /// </param>
    /// <param name="archiveExistingBlob">
    /// When <c>true</c> and a blob at <paramref name="blobPath"/> already exists, it is
    /// timestamped and moved to the archive path before the new blob is uploaded.
    /// </param>
    /// <param name="archivePath">
    /// Blob path prefix for the archived blob, used when <paramref name="archiveExistingBlob"/> is <c>true</c>.
    /// When <c>null</c>, defaults to an <c>archive</c> folder inside the blob's current directory.
    /// </param>
    /// <param name="metadataHeader">
    /// Optional metadata lines written before the encoding and column header rows.
    /// Entries are written in ascending key order; gaps in the key sequence are ignored.
    /// </param>
    /// <param name="writeEncodingHeader">When <c>true</c>, writes the encoding as the first line of the blob content.</param>
    /// <param name="encodingHeaderOverride">
    /// Overrides the encoding string written when <paramref name="writeEncodingHeader"/> is <c>true</c>.
    /// When <c>null</c>, defaults to <see cref="Encoding.WebName"/> (e.g. <c>"utf-8"</c>).
    /// </param>
    /// <returns>A list of error messages. An empty list indicates a successful export.</returns>
    Task<List<string>> ExportToBlobAsync(
        string blobConnectionString,
        string containerName,
        string blobPath,
        Func<Task<List<T>>> dataProvider,
        Encoding encoding,
        CsvConfiguration csvConfiguration,
        bool archiveExistingBlob = false,
        string? archivePath = null,
        IReadOnlyDictionary<int, string>? metadataHeader = null,
        bool writeEncodingHeader = false,
        string? encodingHeaderOverride = null);
}
