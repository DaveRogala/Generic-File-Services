namespace GenericFileServices.Contracts;

/// <summary>
/// Writes delimited files to a local/network path or an Azure Blob Storage container
/// using CsvHelper for serialisation. Column headers and field formatting are derived
/// from the record or class type <typeparamref name="T"/> via CsvHelper's auto-map
/// or a registered <c>ClassMap&lt;T&gt;</c>.
/// </summary>
public interface IFileWriterServices
{
    /// <summary>
    /// Serialises <paramref name="records"/> to a delimited file at
    /// <c><paramref name="basePath"/>/<paramref name="fileName"/></c>.
    /// The directory must exist before calling this method.
    /// </summary>
    /// <typeparam name="T">Record or class type. CsvHelper maps its properties to CSV columns.</typeparam>
    /// <param name="basePath">Target directory (local or UNC path).</param>
    /// <param name="fileName">Name of the output file, e.g. <c>"orders_20260101.csv"</c>.</param>
    /// <param name="records">Data records to serialise.</param>
    /// <param name="encoding">Character encoding. Defaults to UTF-8 without BOM when not specified.</param>
    /// <param name="delimiter">Column delimiter. Defaults to <c>","</c>.</param>
    /// <param name="metadataHeader">
    /// Optional metadata lines to write before the encoding and column header rows.
    /// Entries are written in ascending key order; gaps in the key sequence are ignored.
    /// </param>
    /// <param name="writeHeader">When <c>true</c> (the default), writes a column header row derived from <typeparamref name="T"/>. Set to <c>false</c> to produce a data-only file.</param>
    /// <param name="writeEncodingHeader">When <c>true</c>, writes the encoding as the first line of the file.</param>
    /// <param name="encodingHeaderOverride">
    /// Overrides the encoding string written when <paramref name="writeEncodingHeader"/> is <c>true</c>.
    /// When <c>null</c>, defaults to <see cref="Encoding.WebName"/> (e.g. <c>"utf-8"</c>).
    /// </param>
    void WriteToFile<T>(
        string basePath,
        string fileName,
        IEnumerable<T> records,
        Encoding encoding,
        string delimiter = ",",
        IReadOnlyDictionary<int, string>? metadataHeader = null,
        bool writeHeader = true,
        bool writeEncodingHeader = false,
        string? encodingHeaderOverride = null);

    /// <summary>
    /// Serialises <paramref name="records"/> to a delimited file at
    /// <c><paramref name="basePath"/>/<paramref name="fileName"/></c>
    /// using a caller-supplied <see cref="CsvConfiguration"/>.
    /// Use this overload when you need full control over CsvHelper behaviour —
    /// for example, to force quoting on specific columns via <see cref="CsvConfiguration.ShouldQuote"/>,
    /// register a <c>ClassMap&lt;T&gt;</c>, or configure any other CsvHelper setting.
    /// <c>Delimiter</c> and <c>HasHeaderRecord</c> must be set on <paramref name="csvConfiguration"/>
    /// directly; the convenience parameters from the other overload are not available here.
    /// The directory must exist before calling this method.
    /// </summary>
    /// <typeparam name="T">Record or class type. CsvHelper maps its properties to CSV columns.</typeparam>
    /// <param name="basePath">Target directory (local or UNC path).</param>
    /// <param name="fileName">Name of the output file, e.g. <c>"orders_20260101.csv"</c>.</param>
    /// <param name="records">Data records to serialise.</param>
    /// <param name="encoding">Character encoding.</param>
    /// <param name="csvConfiguration">
    /// CsvHelper configuration passed directly to <see cref="CsvWriter"/>.
    /// The caller is responsible for setting all relevant options, including
    /// <see cref="CsvConfiguration.Delimiter"/> and <see cref="CsvConfiguration.HasHeaderRecord"/>.
    /// </param>
    /// <param name="metadataHeader">
    /// Optional metadata lines to write before the encoding and column header rows.
    /// Entries are written in ascending key order; gaps in the key sequence are ignored.
    /// </param>
    /// <param name="writeEncodingHeader">When <c>true</c>, writes the encoding as the first line of the file.</param>
    /// <param name="encodingHeaderOverride">
    /// Overrides the encoding string written when <paramref name="writeEncodingHeader"/> is <c>true</c>.
    /// When <c>null</c>, defaults to <see cref="Encoding.WebName"/> (e.g. <c>"utf-8"</c>).
    /// </param>
    void WriteToFile<T>(
        string basePath,
        string fileName,
        IEnumerable<T> records,
        Encoding encoding,
        CsvConfiguration csvConfiguration,
        IReadOnlyDictionary<int, string>? metadataHeader = null,
        bool writeEncodingHeader = false,
        string? encodingHeaderOverride = null);

    /// <summary>
    /// Serialises <paramref name="records"/> and uploads the result as a blob to
    /// <paramref name="containerName"/> at <paramref name="blobPath"/>.
    /// An existing blob at the same path is overwritten.
    /// </summary>
    /// <typeparam name="T">Record or class type. CsvHelper maps its properties to CSV columns.</typeparam>
    /// <param name="blobConnectionString">Azure Storage connection string.</param>
    /// <param name="containerName">Target blob container name.</param>
    /// <param name="blobPath">Full blob path, e.g. <c>"exports/orders_20260101.csv"</c>.</param>
    /// <param name="records">Data records to serialise.</param>
    /// <param name="encoding">Character encoding. Defaults to UTF-8 without BOM when not specified.</param>
    /// <param name="delimiter">Column delimiter. Defaults to <c>","</c>.</param>
    /// <param name="metadataHeader">
    /// Optional metadata lines to write before the encoding and column header rows.
    /// Entries are written in ascending key order; gaps in the key sequence are ignored.
    /// </param>
    /// <param name="writeHeader">When <c>true</c> (the default), writes a column header row derived from <typeparamref name="T"/>. Set to <c>false</c> to produce a data-only blob.</param>
    /// <param name="writeEncodingHeader">When <c>true</c>, writes the encoding as the first line of the blob content.</param>
    /// <param name="encodingHeaderOverride">
    /// Overrides the encoding string written when <paramref name="writeEncodingHeader"/> is <c>true</c>.
    /// When <c>null</c>, defaults to <see cref="Encoding.WebName"/> (e.g. <c>"utf-8"</c>).
    /// </param>
    Task WriteToBlobAsync<T>(
        string blobConnectionString,
        string containerName,
        string blobPath,
        IEnumerable<T> records,
        Encoding encoding,
        string delimiter = ",",
        IReadOnlyDictionary<int, string>? metadataHeader = null,
        bool writeHeader = true,
        bool writeEncodingHeader = false,
        string? encodingHeaderOverride = null);

    /// <summary>
    /// Serialises <paramref name="records"/> and uploads the result as a blob to
    /// <paramref name="containerName"/> at <paramref name="blobPath"/>
    /// using a caller-supplied <see cref="CsvConfiguration"/>.
    /// Use this overload when you need full control over CsvHelper behaviour —
    /// for example, to force quoting on specific columns via <see cref="CsvConfiguration.ShouldQuote"/>,
    /// register a <c>ClassMap&lt;T&gt;</c>, or configure any other CsvHelper setting.
    /// An existing blob at the same path is overwritten.
    /// </summary>
    /// <typeparam name="T">Record or class type. CsvHelper maps its properties to CSV columns.</typeparam>
    /// <param name="blobConnectionString">Azure Storage connection string.</param>
    /// <param name="containerName">Target blob container name.</param>
    /// <param name="blobPath">Full blob path, e.g. <c>"exports/orders_20260101.csv"</c>.</param>
    /// <param name="records">Data records to serialise.</param>
    /// <param name="encoding">Character encoding.</param>
    /// <param name="csvConfiguration">
    /// CsvHelper configuration passed directly to <see cref="CsvWriter"/>.
    /// The caller is responsible for setting all relevant options, including
    /// <see cref="CsvConfiguration.Delimiter"/> and <see cref="CsvConfiguration.HasHeaderRecord"/>.
    /// </param>
    /// <param name="metadataHeader">
    /// Optional metadata lines to write before the encoding and column header rows.
    /// Entries are written in ascending key order; gaps in the key sequence are ignored.
    /// </param>
    /// <param name="writeEncodingHeader">When <c>true</c>, writes the encoding as the first line of the blob content.</param>
    /// <param name="encodingHeaderOverride">
    /// Overrides the encoding string written when <paramref name="writeEncodingHeader"/> is <c>true</c>.
    /// When <c>null</c>, defaults to <see cref="Encoding.WebName"/> (e.g. <c>"utf-8"</c>).
    /// </param>
    Task WriteToBlobAsync<T>(
        string blobConnectionString,
        string containerName,
        string blobPath,
        IEnumerable<T> records,
        Encoding encoding,
        CsvConfiguration csvConfiguration,
        IReadOnlyDictionary<int, string>? metadataHeader = null,
        bool writeEncodingHeader = false,
        string? encodingHeaderOverride = null);

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

    /// <summary>
    /// If a blob at <paramref name="blobPath"/> exists in <paramref name="containerName"/>,
    /// copies it to the archive path with a UTC timestamp appended to the file stem, then
    /// deletes the original. Does nothing when the blob does not exist.
    /// </summary>
    /// <param name="blobConnectionString">Azure Storage connection string.</param>
    /// <param name="containerName">Container that holds the blob to archive.</param>
    /// <param name="blobPath">Current blob path, e.g. <c>"exports/orders.csv"</c>.</param>
    /// <param name="archivePath">
    /// Blob path prefix for the archived blob.
    /// When <c>null</c>, defaults to an <c>archive</c> folder inside the blob's current directory,
    /// e.g. <c>"exports/archive"</c>.
    /// </param>
    Task ArchiveExistingBlobAsync(string blobConnectionString, string containerName, string blobPath, string? archivePath = null);
}
