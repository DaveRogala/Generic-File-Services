namespace GenericFileServices.Services;

/// <summary>
/// Default implementation of <see cref="IFileWriterServices"/>.
/// Uses CsvHelper for CSV serialisation and writes to a local/network path
/// or an Azure Blob Storage container.
/// </summary>
public class FileWriterServices(ILogger<FileWriterServices> logger, IBlobClientFactory blobClientFactory) : IFileWriterServices
{
    private readonly ILogger<FileWriterServices> _logger = logger;
    private readonly IBlobClientFactory _blobClientFactory = blobClientFactory;

    /// <summary>
    /// Default <see cref="CsvConfiguration"/> used when no delimiter or header overrides are needed
    /// (comma delimiter, header row written). Cached to avoid per-call allocations.
    /// </summary>
    private static readonly CsvConfiguration DefaultCsvConfiguration =
        new(System.Globalization.CultureInfo.InvariantCulture) { Delimiter = ",", HasHeaderRecord = true };

    /// <inheritdoc/>
    /// <remarks>
    /// Argument validation (e.g. null path) throws synchronously from this method rather than
    /// being wrapped in a faulted <see cref="Task"/>. Callers that depend on await-only error
    /// propagation should use the <see cref="CsvConfiguration"/> overload directly.
    /// </remarks>
    public void WriteToFile<T>(
        string basePath,
        string fileName,
        IEnumerable<T> records,
        Encoding encoding,
        string delimiter = ",",
        IReadOnlyDictionary<int, string>? metadataHeader = null,
        bool writeHeader = true,
        bool writeEncodingHeader = false,
        string? encodingHeaderOverride = null)
    {
        var config = delimiter == "," && writeHeader
            ? DefaultCsvConfiguration
            : new CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture) { Delimiter = delimiter, HasHeaderRecord = writeHeader };
        WriteToFile(basePath, fileName, records, encoding, config, metadataHeader, writeEncodingHeader, encodingHeaderOverride);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Argument validation (e.g. null path) throws synchronously from this method rather than
    /// being wrapped in a faulted <see cref="Task"/>. Callers that depend on await-only error
    /// propagation should use the <see cref="CsvConfiguration"/> overload directly.
    /// </remarks>
    public Task WriteToBlobAsync<T>(
        string blobConnectionString,
        string containerName,
        string blobPath,
        IEnumerable<T> records,
        Encoding encoding,
        string delimiter = ",",
        IReadOnlyDictionary<int, string>? metadataHeader = null,
        bool writeHeader = true,
        bool writeEncodingHeader = false,
        string? encodingHeaderOverride = null)
    {
        var config = delimiter == "," && writeHeader
            ? DefaultCsvConfiguration
            : new CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture) { Delimiter = delimiter, HasHeaderRecord = writeHeader };
        return WriteToBlobAsync(blobConnectionString, containerName, blobPath, records, encoding, config, metadataHeader, writeEncodingHeader, encodingHeaderOverride);
    }

    /// <inheritdoc/>
    public void WriteToFile<T>(
        string basePath,
        string fileName,
        IEnumerable<T> records,
        Encoding encoding,
        CsvConfiguration csvConfiguration,
        IReadOnlyDictionary<int, string>? metadataHeader = null,
        bool writeEncodingHeader = false,
        string? encodingHeaderOverride = null)
    {
        var path = ValidateCombinedPath(basePath, fileName);
        _logger.LogInformation("Writing export file {FileName} to {BasePath}", fileName, basePath);
        using var writer = new StreamWriter(path, append: false, encoding);
        if (metadataHeader is not null)
            foreach (var kvp in metadataHeader.OrderBy(k => k.Key))
                writer.WriteLine(kvp.Value.ReplaceLineEndings(" "));
        if (writeEncodingHeader)
            writer.WriteLine(encodingHeaderOverride ?? encoding.WebName);
        using var csv = new CsvWriter(writer, csvConfiguration);
        csv.WriteRecords(records);
    }

    /// <inheritdoc/>
    public async Task WriteToBlobAsync<T>(
        string blobConnectionString,
        string containerName,
        string blobPath,
        IEnumerable<T> records,
        Encoding encoding,
        CsvConfiguration csvConfiguration,
        IReadOnlyDictionary<int, string>? metadataHeader = null,
        bool writeEncodingHeader = false,
        string? encodingHeaderOverride = null)
    {
        _logger.LogInformation("Writing export blob {BlobPath} to container {ContainerName}", blobPath, containerName);
        var blobClient = _blobClientFactory.GetBlobClient(blobConnectionString, containerName, blobPath);

        using var stream = new MemoryStream();
        await using (var writer = new StreamWriter(stream, encoding, leaveOpen: true))
        {
            if (metadataHeader is not null)
                foreach (var kvp in metadataHeader.OrderBy(k => k.Key))
                    await writer.WriteLineAsync(kvp.Value.ReplaceLineEndings(" "));
            if (writeEncodingHeader)
                await writer.WriteLineAsync(encodingHeaderOverride ?? encoding.WebName);
            await using var csv = new CsvWriter(writer, csvConfiguration);
            csv.WriteRecords(records);
            await csv.FlushAsync();
        }
        stream.Position = 0;
        await blobClient.UploadAsync(stream, overwrite: true);
    }

    /// <inheritdoc/>
    public void ArchiveExistingFile(string basePath, string fileName, string? archivePath = null)
    {
        var sourcePath = ValidateCombinedPath(basePath, fileName);
        if (!File.Exists(sourcePath))
            return;

        var resolvedArchivePath = archivePath is null
            ? Path.Combine(basePath, "archive")
            : Path.IsPathRooted(archivePath)
                ? archivePath
                : Path.Combine(basePath, archivePath);

        Directory.CreateDirectory(resolvedArchivePath);

        var timeStamp = DateTime.UtcNow.ToString(Constants.TimestampFormat);
        var name = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        var destPath = Path.Combine(resolvedArchivePath, $"{name}_{timeStamp}{ext}");

        File.Move(sourcePath, destPath);
        _logger.LogInformation("Archived {FileName} to {DestPath}", fileName, destPath);
    }

    /// <inheritdoc/>
    public async Task ArchiveExistingBlobAsync(
        string blobConnectionString,
        string containerName,
        string blobPath,
        string? archivePath = null)
    {
        var blobClient = _blobClientFactory.GetBlobClient(blobConnectionString, containerName, blobPath);

        if (!await blobClient.ExistsAsync())
            return;

        var timestamp = DateTime.UtcNow.ToString(Constants.TimestampFormat);
        var name = Path.GetFileNameWithoutExtension(blobPath);
        var ext = Path.GetExtension(blobPath);
        var archivedFileName = $"{name}_{timestamp}{ext}";

        string resolvedArchiveDir;
        if (archivePath is null)
        {
            var lastSlash = blobPath.LastIndexOf('/');
            var blobDir = lastSlash >= 0 ? blobPath[..lastSlash] : "";
            resolvedArchiveDir = blobDir.Length > 0 ? $"{blobDir}/archive" : "archive";
        }
        else
        {
            resolvedArchiveDir = archivePath.TrimEnd('/');
        }

        var archiveBlobPath = $"{resolvedArchiveDir}/{archivedFileName}";
        var archiveBlobClient = _blobClientFactory.GetBlobClient(blobConnectionString, containerName, archiveBlobPath);

        await archiveBlobClient.SyncCopyFromUriAsync(blobClient.Uri);
        await blobClient.DeleteAsync();

        _logger.LogInformation("Archived blob {BlobPath} to {ArchiveBlobPath}", blobPath, archiveBlobPath);
    }

    /// <summary>
    /// Combines <paramref name="basePath"/> and <paramref name="fileName"/> and validates
    /// that the resulting full path stays within <paramref name="basePath"/> to prevent
    /// path traversal attacks.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Thrown when the resolved path escapes <paramref name="basePath"/>.
    /// </exception>
    private static string ValidateCombinedPath(string basePath, string fileName)
    {
        var combined = Path.Combine(basePath, fileName);
        var fullCombined = Path.GetFullPath(combined);
        var fullBase = Path.GetFullPath(basePath);

        if (!fullCombined.StartsWith(fullBase + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !string.Equals(fullCombined, fullBase, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Resolved path '{fullCombined}' escapes the base directory '{fullBase}'.",
                nameof(fileName));
        }

        return combined;
    }
}
