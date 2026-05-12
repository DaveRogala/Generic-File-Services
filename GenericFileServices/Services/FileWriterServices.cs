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
    private static readonly string TimestampFormat = "yyyyMMddHHmmssfff";

    /// <inheritdoc/>
    public void WriteToFile<T>(
        string basePath,
        string fileName,
        IEnumerable<T> records,
        Encoding encoding,
        string delimiter = ",")
    {
        var path = Path.Combine(basePath, fileName);
        _logger.LogInformation("Writing export file {FileName} to {BasePath}", fileName, basePath);
        using var writer = new StreamWriter(path, append: false, encoding);
        using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture) { Delimiter = delimiter });
        csv.WriteRecords(records);
    }

    /// <inheritdoc/>
    public async Task WriteToBlobAsync<T>(
        string blobConnectionString,
        string containerName,
        string blobPath,
        IEnumerable<T> records,
        Encoding encoding,
        string delimiter = ",")
    {
        _logger.LogInformation("Writing export blob {BlobPath} to container {ContainerName}", blobPath, containerName);
        var blobClient = _blobClientFactory.GetBlobClient(blobConnectionString, containerName, blobPath);

        using var stream = new MemoryStream();
        await using (var writer = new StreamWriter(stream, encoding, leaveOpen: true))
        {
            using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture) { Delimiter = delimiter });
            csv.WriteRecords(records);
        }
        stream.Position = 0;
        await blobClient.UploadAsync(stream, overwrite: true);
    }

    /// <inheritdoc/>
    public void ArchiveExistingFile(string basePath, string fileName, string? archivePath = null)
    {
        var sourcePath = Path.Combine(basePath, fileName);
        if (!File.Exists(sourcePath))
            return;

        var resolvedArchivePath = archivePath is null
            ? Path.Combine(basePath, "archive")
            : Path.IsPathRooted(archivePath)
                ? archivePath
                : Path.Combine(basePath, archivePath);

        Directory.CreateDirectory(resolvedArchivePath);

        var timeStamp = DateTime.UtcNow.ToString(TimestampFormat);
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

        var timestamp = DateTime.UtcNow.ToString(TimestampFormat);
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

        using var stream = new MemoryStream();
        await blobClient.DownloadToAsync(stream);
        stream.Position = 0;
        await archiveBlobClient.UploadAsync(stream, overwrite: true);
        await blobClient.DeleteAsync();

        _logger.LogInformation("Archived blob {BlobPath} to {ArchiveBlobPath}", blobPath, archiveBlobPath);
    }
}
