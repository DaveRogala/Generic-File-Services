namespace GenericFileServices.Services;

/// <summary>
/// Default implementation of <see cref="IFileExportServices{T,C}"/>.
/// Fetches data via a consumer-supplied delegate and delegates serialisation
/// and writing to <see cref="IFileWriterServices"/>.
/// Argument validation errors propagate as <see cref="ArgumentException"/>;
/// data-provider and writer errors are caught and returned in the errors list.
/// </summary>
/// <typeparam name="T">DTO or record type returned by the data provider.</typeparam>
/// <typeparam name="C">EF Core <see cref="DbContext"/> type used by the consuming application.</typeparam>
public class FileExportServices<T, C>(
    IFileWriterServices fileWriterServices,
    ILogger<FileExportServices<T, C>> logger) : IFileExportServices<T, C>
    where T : class
    where C : DbContext
{
    private readonly IFileWriterServices _fileWriterServices = fileWriterServices;
    private readonly ILogger<FileExportServices<T, C>> _logger = logger;

    /// <inheritdoc/>
    public async Task<List<string>> ExportToFileAsync(
        string basePath,
        string fileName,
        Func<Task<List<T>>> dataProvider,
        Encoding encoding,
        string delimiter = ",",
        bool archiveExistingFile = false,
        string? archivePath = null,
        bool writeHeader = true,
        bool writeEncodingHeader = false,
        string? encodingHeaderOverride = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(basePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var errors = new List<string>();
        try
        {
            if (archiveExistingFile)
                _fileWriterServices.ArchiveExistingFile(basePath, fileName, archivePath);

            var data = await dataProvider();
            _fileWriterServices.WriteToFile(basePath, fileName, data, encoding, delimiter, writeHeader, writeEncodingHeader, encodingHeaderOverride);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting {FileName} to {BasePath}", fileName, basePath);
            errors.Add($"Export of {fileName} failed: {ex.Message}");
        }
        return errors;
    }

    /// <inheritdoc/>
    public async Task<List<string>> ExportToBlobAsync(
        string blobConnectionString,
        string containerName,
        string blobPath,
        Func<Task<List<T>>> dataProvider,
        Encoding encoding,
        string delimiter = ",",
        bool archiveExistingBlob = false,
        string? archivePath = null,
        bool writeHeader = true,
        bool writeEncodingHeader = false,
        string? encodingHeaderOverride = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blobConnectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(blobPath);

        var errors = new List<string>();
        try
        {
            if (archiveExistingBlob)
                await _fileWriterServices.ArchiveExistingBlobAsync(blobConnectionString, containerName, blobPath, archivePath);

            var data = await dataProvider();
            await _fileWriterServices.WriteToBlobAsync(blobConnectionString, containerName, blobPath, data, encoding, delimiter, writeHeader, writeEncodingHeader, encodingHeaderOverride);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting to blob {BlobPath}", blobPath);
            errors.Add($"Export to blob {blobPath} failed: {ex.Message}");
        }
        return errors;
    }
}
