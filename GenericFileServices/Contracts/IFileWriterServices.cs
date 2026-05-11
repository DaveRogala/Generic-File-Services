namespace GenericFileServices.Contracts;

/// <summary>
/// Writes delimited files to a local/network path or an Azure Blob Storage container.
/// Each row is supplied as a sequence of field values; the implementation handles
/// delimiter escaping and field quoting.
/// </summary>
public interface IFileWriterServices
{
    /// <summary>
    /// Creates a delimited file at <c><paramref name="basePath"/>/<paramref name="fileName"/></c>.
    /// The directory must exist before calling this method.
    /// </summary>
    /// <param name="basePath">Target directory (local or UNC path).</param>
    /// <param name="fileName">Name of the output file, e.g. <c>"orders_20260101.csv"</c>.</param>
    /// <param name="headers">Column header names written as the first row. Pass an empty sequence to omit the header.</param>
    /// <param name="rows">Data rows; each inner sequence must contain the same number of fields as <paramref name="headers"/>.</param>
    /// <param name="encoding">Character encoding. Defaults to UTF-8 without BOM when not specified.</param>
    /// <param name="delimiter">Column delimiter. Defaults to <c>","</c>.</param>
    void WriteToFile(
        string basePath,
        string fileName,
        IEnumerable<string> headers,
        IEnumerable<IEnumerable<string>> rows,
        Encoding encoding,
        string delimiter = ",");

    /// <summary>
    /// Uploads a delimited file as a blob to <paramref name="containerName"/> at <paramref name="blobPath"/>.
    /// An existing blob at the same path is overwritten.
    /// </summary>
    /// <param name="blobConnectionString">Azure Storage connection string.</param>
    /// <param name="containerName">Target blob container name.</param>
    /// <param name="blobPath">Full blob path, e.g. <c>"exports/orders_20260101.csv"</c>.</param>
    /// <param name="headers">Column header names written as the first row. Pass an empty sequence to omit the header.</param>
    /// <param name="rows">Data rows; each inner sequence must contain the same number of fields as <paramref name="headers"/>.</param>
    /// <param name="encoding">Character encoding. Defaults to UTF-8 without BOM when not specified.</param>
    /// <param name="delimiter">Column delimiter. Defaults to <c>","</c>.</param>
    /// <summary>
    /// If a file named <paramref name="fileName"/> already exists in <paramref name="basePath"/>,
    /// moves it to the archive directory with a UTC timestamp appended to the file stem.
    /// Does nothing when the file does not exist.
    /// The archive directory is created automatically if it does not exist.
    /// </summary>
    /// <param name="basePath">Directory containing the file to archive.</param>
    /// <param name="fileName">Name of the file to archive, e.g. <c>"export.csv"</c>.</param>
    /// <param name="archivePath">
    /// Destination directory for the archived file.
    /// Absolute paths are used as-is; relative paths are resolved relative to <paramref name="basePath"/>.
    /// When <c>null</c>, defaults to an <c>archive</c> sub-folder of <paramref name="basePath"/>.
    /// </param>
    void ArchiveExistingFile(string basePath, string fileName, string? archivePath = null);

    Task WriteToBlobAsync(
        string blobConnectionString,
        string containerName,
        string blobPath,
        IEnumerable<string> headers,
        IEnumerable<IEnumerable<string>> rows,
        Encoding encoding,
        string delimiter = ",");
}
