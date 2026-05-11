namespace GenericFileServices.Contracts;

/// <summary>
/// Reads delimited files from disk or a stream and returns typed parse results,
/// and handles file archiving and error reporting after processing.
/// </summary>
/// <typeparam name="U">DTO type that each parsed row maps to.</typeparam>
public interface IFileReaderServices<U>
{
    /// <summary>
    /// Reads all files in <paramref name="basePath"/> matching <paramref name="fileNamePattern"/>,
    /// parsing each into a list of <typeparamref name="U"/> DTOs.
    /// Files are processed in ascending last-write-time order.
    /// </summary>
    /// <param name="basePath">Directory to search.</param>
    /// <param name="fileNamePattern">Wildcard pattern, e.g. <c>"orders_*.csv"</c>.</param>
    /// <param name="encoding">Character encoding of the file.</param>
    /// <param name="delimiter">Column delimiter. Defaults to <c>","</c>.</param>
    /// <param name="firstLineContainsEncoding">Whether the first data line is a header row.</param>
    /// <param name="failIfFileMissing">Throw when no files match <paramref name="fileNamePattern"/>.</param>
    /// <param name="multipleFiles">Allow more than one matching file. Throws when <c>false</c> and multiple files are found.</param>
    /// <param name="rowsToSkip">Number of leading rows to skip before parsing begins.</param>
    /// <param name="fixUnescapedQuotes">Attempt to repair unescaped quote characters in CSV fields.</param>
    List<FileResults<U>> ReadFromFile(string basePath, string fileNamePattern, Encoding encoding, string delimiter = ",", bool firstLineContainsEncoding = false, bool failIfFileMissing = true, bool multipleFiles = false, int rowsToSkip = 0, bool fixUnescapedQuotes = false);

    /// <summary>
    /// Simplified overload using <see cref="Encoding.Default"/> and comma delimiter.
    /// </summary>
    /// <param name="basePath">Directory to search.</param>
    /// <param name="fileNamePattern">Wildcard pattern.</param>
    /// <param name="firstLineContainsEncoding">Whether the first data line is a header row.</param>
    /// <param name="failIfFileMissing">Throw when no files match the pattern.</param>
    List<FileResults<U>> ReadFromFile(string basePath, string fileNamePattern, bool firstLineContainsEncoding, bool failIfFileMissing = true);

    /// <summary>
    /// Reads from an already-opened <see cref="Stream"/> (e.g. an Azure Blob stream).
    /// </summary>
    /// <param name="stream">Readable stream positioned at the start of the file.</param>
    /// <param name="fileName">Logical file name used for result tracking and error reporting.</param>
    /// <param name="encoding">Character encoding of the stream content.</param>
    /// <param name="firstLineContainsEncoding">Whether the first data line is a header row.</param>
    /// <param name="delimiter">Column delimiter. Defaults to <c>","</c>.</param>
    /// <param name="rowsToSkip">Number of leading rows to skip before parsing begins.</param>
    /// <param name="fixUnescapedQuotes">Attempt to repair unescaped quote characters in CSV fields.</param>
    List<FileResults<U>> ReadFromFile(Stream stream, string fileName, Encoding encoding, bool firstLineContainsEncoding, string delimiter = ",", int rowsToSkip = 0, bool fixUnescapedQuotes = false);

    /// <summary>Moves or renames a file to the error location and optionally records per-row error messages.</summary>
    /// <param name="basePath">Directory containing the file.</param>
    /// <param name="fileName">Name of the file that failed.</param>
    /// <param name="exceptionMessage">Top-level error description.</param>
    /// <param name="timeStamp">Timestamp string appended to the archived file name.</param>
    /// <param name="errors">Optional per-row error messages.</param>
    void HandleFileError(string basePath, string fileName, string exceptionMessage, string timeStamp, List<string>? errors = null);

    /// <summary>Moves or renames a blob to the error location and optionally records per-row error messages.</summary>
    Task HandleFileErrorAsync(Stream stream, string blobConnectionString, string containerName, string filePath, string exceptionMessage, string timeStamp, List<string>? errors = null);

    /// <summary>Moves or renames a successfully processed file to the archive location.</summary>
    /// <param name="basePath">Directory containing the file.</param>
    /// <param name="fileName">Name of the file to archive.</param>
    /// <param name="timeStamp">Timestamp string appended to the archived file name.</param>
    void HandleFileSuccess(string basePath, string fileName, string timeStamp);

    /// <summary>Moves or renames a successfully processed blob to the archive location.</summary>
    Task HandleFileSuccessAsync(Stream stream, string blobConnectionString, string containerName, string filePath, string timeStamp);
}
