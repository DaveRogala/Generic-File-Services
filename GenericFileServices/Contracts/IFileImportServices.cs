namespace GenericFileServices.Contracts;

/// <summary>
/// Orchestrates a complete file-to-database ETL import: file discovery, parsing,
/// database reconciliation, and file archiving or error handling.
/// </summary>
/// <typeparam name="T">Entity type persisted to the database. Must extend <see cref="GenericFileServices.Models.Database.Base.BaseObject"/>.</typeparam>
/// <typeparam name="U">DTO type that each parsed file row maps to.</typeparam>
/// <typeparam name="C">EF Core <see cref="DbContext"/> type.</typeparam>
public interface IFileImportServices<T, U, C>
    where T : BaseObject
    where C : DbContext
{
    /// <summary>
    /// Discovers files on disk matching <paramref name="fileNamePattern"/>, reconciles each with the database,
    /// then archives or reports errors per file.
    /// </summary>
    /// <param name="basePath">Directory to search.</param>
    /// <param name="fileNamePattern">Wildcard pattern, e.g. <c>"orders_*.csv"</c>.</param>
    /// <param name="options">Import configuration. Pass <see cref="FileImportOptions.Default"/> for default behaviour.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A list of error messages. An empty list indicates a fully successful import.</returns>
    Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, FileImportOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads from an already-opened blob stream, reconciles with the database, then archives or reports errors.
    /// </summary>
    /// <param name="stream">Readable stream positioned at the start of the file.</param>
    /// <param name="blobConnectionString">Azure Storage connection string.</param>
    /// <param name="containerName">Blob container name.</param>
    /// <param name="filePath">Full blob path, used for archiving and error reporting.</param>
    /// <param name="options">Import configuration. Pass <see cref="FileImportOptions.Default"/> for default behaviour.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A list of error messages. An empty list indicates a fully successful import.</returns>
    Task<List<string>> ProcessFileAsync(Stream stream, string blobConnectionString, string containerName, string filePath, FileImportOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads from an already-opened blob stream, reconciles with the database, then archives or reports errors.
    /// </summary>
    /// <param name="stream">Readable stream positioned at the start of the file.</param>
    /// <param name="blobConnectionString">Azure Storage connection string.</param>
    /// <param name="containerName">Blob container name.</param>
    /// <param name="filePath">Full blob path, used for archiving and error reporting.</param>
    /// <param name="encoding">Character encoding of the stream content.</param>
    /// <param name="delimiter">Column delimiter. Defaults to <c>","</c>.</param>
    /// <param name="firstLineContainsEncoding">Whether the first data line is a header row.</param>
    /// <param name="failIfNotFound">Throw when the stream yields no results.</param>
    /// <param name="multipleFiles">Reserved for future multi-blob scenarios.</param>
    /// <param name="archiveIfSuccess">Move the blob to the archive location on a clean import.</param>
    /// <param name="hardDelete">Permanently delete; <c>false</c> sets <c>DateDeletedUtc</c> (soft delete).</param>
    /// <param name="rowsToSkip">Number of leading rows to skip before parsing begins.</param>
    /// <param name="fixUnescapedQuotes">Attempt to repair unescaped quote characters in CSV fields.</param>
    /// <returns>A list of error messages. An empty list indicates a fully successful import.</returns>
    [Obsolete("Use ProcessFileAsync(Stream, string, string, string, FileImportOptions) instead.")]
    Task<List<string>> ProcessFileAsync(Stream stream, string blobConnectionString, string containerName, string filePath, Encoding encoding, string delimiter = ",", bool firstLineContainsEncoding = false, bool failIfNotFound = true, bool multipleFiles = false, bool archiveIfSuccess = true, bool hardDelete = false, int rowsToSkip = 0, bool fixUnescapedQuotes = false);

    /// <summary>
    /// Discovers files on disk matching <paramref name="fileNamePattern"/>, reconciles each with the database,
    /// then archives or reports errors per file.
    /// </summary>
    /// <param name="basePath">Directory to search.</param>
    /// <param name="fileNamePattern">Wildcard pattern, e.g. <c>"orders_*.csv"</c>.</param>
    /// <param name="encoding">Character encoding of the file.</param>
    /// <param name="delimiter">Column delimiter. Defaults to <c>","</c>.</param>
    /// <param name="firstLineContainsEncoding">Whether the first data line is a header row.</param>
    /// <param name="failIfNotFound">Throw when no files match <paramref name="fileNamePattern"/>.</param>
    /// <param name="multipleFiles">Allow more than one matching file.</param>
    /// <param name="archiveIfSuccess">Move the file to the archive location on a clean import.</param>
    /// <param name="hardDelete">Permanently delete; <c>false</c> sets <c>DateDeletedUtc</c> (soft delete).</param>
    /// <param name="rowsToSkip">Number of leading rows to skip before parsing begins.</param>
    /// <param name="fixUnescapedQuotes">Attempt to repair unescaped quote characters in CSV fields.</param>
    /// <returns>A list of error messages. An empty list indicates a fully successful import.</returns>
    [Obsolete("Use ProcessFileAsync(string, string, FileImportOptions) instead.")]
    Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, Encoding encoding, string delimiter = ",", bool firstLineContainsEncoding = false, bool failIfNotFound = true, bool multipleFiles = false, bool archiveIfSuccess = true, bool hardDelete = false, int rowsToSkip = 0, bool fixUnescapedQuotes = false);

    /// <summary>Convenience overload. Skips <paramref name="rowsToSkip"/> leading rows; all other settings use defaults.</summary>
    /// <param name="basePath">Directory to search.</param>
    /// <param name="fileNamePattern">Wildcard pattern.</param>
    /// <param name="rowsToSkip">Number of leading rows to skip before parsing begins.</param>
    /// <param name="archiveIfSuccess">Move the file to the archive location on a clean import.</param>
    [Obsolete("Use ProcessFileAsync(string, string, FileImportOptions) instead.")]
    Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, int rowsToSkip, bool archiveIfSuccess = true);

    /// <summary>Convenience overload. Skips leading rows and optionally repairs unescaped quotes; other settings use defaults.</summary>
    /// <param name="basePath">Directory to search.</param>
    /// <param name="fileNamePattern">Wildcard pattern.</param>
    /// <param name="rowsToSkip">Number of leading rows to skip before parsing begins.</param>
    /// <param name="fixUnescapedQuotes">Attempt to repair unescaped quote characters in CSV fields.</param>
    /// <param name="archiveIfSuccess">Move the file to the archive location on a clean import.</param>
    [Obsolete("Use ProcessFileAsync(string, string, FileImportOptions) instead.")]
    Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, int rowsToSkip, bool fixUnescapedQuotes, bool archiveIfSuccess = true);

    /// <inheritdoc cref="ProcessFileAsync(string,string,Encoding,string,bool,bool,bool,bool,bool,int,bool)"/>
    [Obsolete("Use ProcessFileAsync(string, string, FileImportOptions) instead.")]
    Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, Encoding encoding, string delimiter = ",", bool failIfNotFound = true, bool archiveIfSuccess = true);

    /// <inheritdoc cref="ProcessFileAsync(string,string,Encoding,string,bool,bool,bool,bool,bool,int,bool)"/>
    [Obsolete("Use ProcessFileAsync(string, string, FileImportOptions) instead.")]
    Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, Encoding encoding, string delimiter = ",", bool archiveIfSuccess = true);

    /// <inheritdoc cref="ProcessFileAsync(string,string,Encoding,string,bool,bool,bool,bool,bool,int,bool)"/>
    [Obsolete("Use ProcessFileAsync(string, string, FileImportOptions) instead.")]
    Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, string delimiter = ",", bool failIfNotFound = true, bool multipleFiles = false, bool archiveIfSuccess = true);

    /// <inheritdoc cref="ProcessFileAsync(string,string,Encoding,string,bool,bool,bool,bool,bool,int,bool)"/>
    [Obsolete("Use ProcessFileAsync(string, string, FileImportOptions) instead.")]
    Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, string delimiter = ",", bool failIfNotFound = true, bool archiveIfSuccess = true);

    /// <inheritdoc cref="ProcessFileAsync(string,string,Encoding,string,bool,bool,bool,bool,bool,int,bool)"/>
    [Obsolete("Use ProcessFileAsync(string, string, FileImportOptions) instead.")]
    Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, string delimiter = ",", bool archiveIfSuccess = true);

    /// <inheritdoc cref="ProcessFileAsync(string,string,Encoding,string,bool,bool,bool,bool,bool,int,bool)"/>
    [Obsolete("Use ProcessFileAsync(string, string, FileImportOptions) instead.")]
    Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, bool hardDelete);

    /// <inheritdoc cref="ProcessFileAsync(string,string,Encoding,string,bool,bool,bool,bool,bool,int,bool)"/>
    [Obsolete("Use ProcessFileAsync(string, string, FileImportOptions) instead.")]
    Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, bool firstLineContainsEncoding, bool hardDelete);

    /// <inheritdoc cref="ProcessFileAsync(string,string,Encoding,string,bool,bool,bool,bool,bool,int,bool)"/>
    [Obsolete("Use ProcessFileAsync(string, string, FileImportOptions) instead.")]
    Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, bool failIfNotFound, bool firstLineContainsEncoding, bool archiveIfSuccess);
}
