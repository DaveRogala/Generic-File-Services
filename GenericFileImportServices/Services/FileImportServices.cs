using GenericFileImportServices.Models;

namespace GenericFileImportServices.Services;

/// <summary>
/// Abstract base class for file-to-database ETL import services.
/// Handles file discovery, parsing, database reconciliation, and file archiving or error reporting.
/// Consumers extend this class and implement <see cref="GetAddEntities"/>,
/// <see cref="GetUpdateEntities"/>, and <see cref="GetDeleteEntities"/> to define
/// how file rows map to database changes.
/// </summary>
/// <typeparam name="T">Entity type. Must extend <see cref="GenericFileImportServices.Models.Database.Base.BaseObject"/>.</typeparam>
/// <typeparam name="U">DTO type that each parsed file row maps to.</typeparam>
/// <typeparam name="C">EF Core <see cref="DbContext"/> type.</typeparam>
public abstract class FileImportServices<T, U, C> : IFileImportServices<T, U, C>
    where T : BaseObject
    where C : DbContext
{
    private readonly IDatabaseServices<T, C> _databaseServices;
    private readonly IFileReaderServices<U> _fileReaderServices;
    private readonly ILogger<FileImportServices<T, U, C>> _logger;
    private readonly IFileImportRecordServices<C>? _fileImportRecordServices;

    private static readonly string TimestampFormat = "yyyyMMddHHmmssfff";
    private static readonly string FileContainedErrors = "File contained errors";

    /// <summary>Initializes a new instance with the required collaborators.</summary>
    /// <param name="databaseServices">Persistence service for the entity type.</param>
    /// <param name="fileReaderServices">File parsing and archiving service.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="fileImportRecordServices">
    /// Optional metadata recorder. When supplied, a <see cref="FileImportRecord"/> and
    /// <see cref="FileImportEntityLink"/> rows are written for every successful import.
    /// Omit (or pass <c>null</c>) to skip metadata recording.
    /// </param>
    protected FileImportServices(
        IDatabaseServices<T, C> databaseServices,
        IFileReaderServices<U> fileReaderServices,
        ILogger<FileImportServices<T, U, C>> logger,
        IFileImportRecordServices<C>? fileImportRecordServices = null)
    {
        _fileReaderServices = fileReaderServices;
        _databaseServices = databaseServices;
        _logger = logger;
        _fileImportRecordServices = fileImportRecordServices;
    }

    /// <inheritdoc/>
    public async Task<List<string>> ProcessFileAsync(Stream stream, string blobConnectionString, string containerName, string filePath, Encoding encoding, string delimiter = ",", bool firstLineContainsEncoding = false, bool failIfNotFound = true, bool multipleFiles = false, bool archiveIfSuccess = true, bool hardDelete = false, int rowsToSkip = 0, bool fixUnescapedQuotes = false)
    {
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(blobConnectionString, nameof(blobConnectionString));
            ArgumentException.ThrowIfNullOrWhiteSpace(containerName, nameof(containerName));
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath, nameof(filePath));

            FileResults<U>? fileResult = _fileReaderServices.ReadFromFile(stream, Path.GetFileName(filePath), encoding, firstLineContainsEncoding, delimiter, rowsToSkip, fixUnescapedQuotes).FirstOrDefault();

            if (fileResult is not null)
            {
                List<string> errors = fileResult.Errors;
                string timeStamp = DateTime.UtcNow.ToString(TimestampFormat);

                List<T> addEntities = [];
                List<T> updateEntities = [];

                try
                {
                    if (fileResult.ObjectResults is not null)
                    {
                        List<T> existingEntities = await _databaseServices.GetAllEntitiesAsync();
                        addEntities = GetAddEntities(existingEntities, fileResult.ObjectResults);
                        updateEntities = GetUpdateEntities(existingEntities, fileResult.ObjectResults);

                        await _databaseServices.UpdateDatabaseAsync(
                            addEntities,
                            updateEntities,
                            GetDeleteEntities(existingEntities, fileResult.ObjectResults),
                            hardDelete);
                    }
                    if (fileResult.Errors.Count > 0)
                    {
                        await _fileReaderServices.HandleFileErrorAsync(stream, blobConnectionString, containerName, filePath, FileContainedErrors, timeStamp, fileResult.Errors);
                    }
                    else if (archiveIfSuccess)
                    {
                        await _fileReaderServices.HandleFileSuccessAsync(stream, blobConnectionString, containerName, filePath, timeStamp);
                        if (_fileImportRecordServices is not null)
                        {
                            await _fileImportRecordServices.RecordFileImportAsync(
                                Path.GetFileName(filePath),
                                BuildArchivedBlobPath(filePath, timeStamp),
                                addEntities.Select(e => e.Id).Concat(updateEntities.Select(e => e.Id)));
                        }
                    }
                }
                catch (Exception ex)
                {
                    await _fileReaderServices.HandleFileErrorAsync(stream, blobConnectionString, containerName, filePath, ex.Message, timeStamp, fileResult.Errors);
                    errors.Add($"File: {fileResult.FileName} ({ex.Message})");
                }
                return errors;
            }
            throw new InvalidOperationException("Stream returned no file result");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing blob file {FilePath}", filePath);
            throw;
        }
    }

    /// <inheritdoc/>
    public virtual async Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, Encoding encoding, string delimiter = ",", bool firstLineContainsEncoding = false, bool failIfNotFound = true, bool multipleFiles = false, bool archiveIfSuccess = true, bool hardDelete = false, int rowsToSkip = 0, bool fixUnescapedQuotes = false)
    {
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(basePath, nameof(basePath));
            ArgumentException.ThrowIfNullOrWhiteSpace(fileNamePattern, nameof(fileNamePattern));
            var fileResults = _fileReaderServices.ReadFromFile(basePath, fileNamePattern, encoding, delimiter, firstLineContainsEncoding: firstLineContainsEncoding, failIfFileMissing: failIfNotFound, multipleFiles, rowsToSkip: rowsToSkip, fixUnescapedQuotes: fixUnescapedQuotes);
            List<string> errors = [..fileResults.SelectMany(f => f.Errors.Select(e => $"File: {f.FileName}: Error; {e}"))];

            if (fileResults.Count == 0) return errors;

            List<T> existingEntities = await _databaseServices.GetAllEntitiesAsync();

            foreach (var fileResult in fileResults)
            {
                string timeStamp = DateTime.UtcNow.ToString(TimestampFormat);
                List<T> addEntities = [];
                List<T> updateEntities = [];
                try
                {
                    if (fileResult.ObjectResults is not null)
                    {
                        addEntities = GetAddEntities(existingEntities, fileResult.ObjectResults);
                        updateEntities = GetUpdateEntities(existingEntities, fileResult.ObjectResults);

                        await _databaseServices.UpdateDatabaseAsync(
                            addEntities,
                            updateEntities,
                            GetDeleteEntities(existingEntities, fileResult.ObjectResults),
                            hardDelete);
                    }
                    if (fileResult.Errors.Count > 0)
                    {
                        _fileReaderServices.HandleFileError(basePath, fileResult.FileName, FileContainedErrors, timeStamp, fileResult.Errors);
                    }
                    else if (archiveIfSuccess)
                    {
                        _fileReaderServices.HandleFileSuccess(basePath, fileResult.FileName, timeStamp);
                        if (_fileImportRecordServices is not null)
                        {
                            await _fileImportRecordServices.RecordFileImportAsync(
                                fileResult.FileName,
                                BuildArchivedFileName(fileResult.FileName, timeStamp),
                                addEntities.Select(e => e.Id).Concat(updateEntities.Select(e => e.Id)));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _fileReaderServices.HandleFileError(basePath, fileResult.FileName, ex.Message, timeStamp, fileResult.Errors);
                    errors.Add($"File: {fileResult.FileName} ({ex.Message})");
                }
            }
            return errors;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing files at {BasePath} matching {Pattern}", basePath, fileNamePattern);
            throw;
        }
    }

    /// <summary>
    /// Returns the entities to insert — rows present in <paramref name="dtos"/> but absent from <paramref name="existingEntities"/>.
    /// Override only if the import scenario requires adds; defaults to no-op.
    /// </summary>
    /// <remarks>For large datasets, build a <see cref="HashSet{T}"/> of existing keys before scanning to keep the implementation O(n) rather than O(n²).</remarks>
    public virtual List<T> GetAddEntities(List<T> existingEntities, List<U> dtos) => [];

    /// <summary>
    /// Returns the entities to update — rows present in both <paramref name="existingEntities"/> and <paramref name="dtos"/>,
    /// with field changes already applied to the returned entities.
    /// Override only if the import scenario requires updates; defaults to no-op.
    /// </summary>
    public virtual List<T> GetUpdateEntities(List<T> existingEntities, List<U> dtos) => [];

    /// <summary>
    /// Returns the entities to delete — rows present in <paramref name="existingEntities"/> but absent from <paramref name="dtos"/>.
    /// Override only if the import scenario requires deletes; defaults to no-op.
    /// </summary>
    /// <remarks>For large datasets, build a <see cref="HashSet{T}"/> of DTO keys before scanning to keep the implementation O(n) rather than O(n²).</remarks>
    public virtual List<T> GetDeleteEntities(List<T> existingEntities, List<U> dtos) => [];

    /// <inheritdoc/>
    public async Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, Encoding encoding, string delimiter = ",", bool failIfNotFound = true, bool archiveIfSuccess = true)
    {
        return await ProcessFileAsync(basePath, fileNamePattern, encoding, delimiter, firstLineContainsEncoding: false, failIfNotFound, multipleFiles: false, archiveIfSuccess);
    }

    /// <inheritdoc/>
    public async Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, Encoding encoding, string delimiter = ",", bool archiveIfSuccess = true)
    {
        return await ProcessFileAsync(basePath, fileNamePattern, encoding, delimiter, firstLineContainsEncoding: false, failIfNotFound: true, multipleFiles: false, archiveIfSuccess);
    }

    /// <inheritdoc/>
    public async Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, string delimiter = ",", bool failIfNotFound = true, bool multipleFiles = false, bool archiveIfSuccess = true)
    {
        return await ProcessFileAsync(basePath, fileNamePattern, encoding: Encoding.Default, delimiter, firstLineContainsEncoding: false, failIfNotFound, multipleFiles, archiveIfSuccess);
    }

    /// <inheritdoc/>
    public async Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, string delimiter = ",", bool failIfNotFound = true, bool archiveIfSuccess = true)
    {
        return await ProcessFileAsync(basePath, fileNamePattern, encoding: Encoding.Default, delimiter, firstLineContainsEncoding: false, failIfNotFound, multipleFiles: false, archiveIfSuccess);
    }

    /// <inheritdoc/>
    public async Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, string delimiter = ",", bool archiveIfSuccess = true)
    {
        return await ProcessFileAsync(basePath, fileNamePattern, encoding: Encoding.Default, delimiter, firstLineContainsEncoding: false, failIfNotFound: true, multipleFiles: false, archiveIfSuccess);
    }

    /// <inheritdoc/>
    public async Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, bool hardDelete)
    {
        return await ProcessFileAsync(basePath, fileNamePattern, encoding: Encoding.Default, delimiter: ",", firstLineContainsEncoding: false, failIfNotFound: true, multipleFiles: false, archiveIfSuccess: true, hardDelete);
    }

    /// <inheritdoc/>
    public async Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, bool firstLineContainsEncoding, bool hardDelete)
    {
        return await ProcessFileAsync(basePath, fileNamePattern, encoding: Encoding.Default, delimiter: ",", firstLineContainsEncoding, failIfNotFound: true, multipleFiles: false, archiveIfSuccess: true, hardDelete);
    }

    /// <inheritdoc/>
    public async Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, bool failIfNotFound, bool firstLineContainsEncoding, bool archiveIfSuccess)
    {
        return await ProcessFileAsync(basePath, fileNamePattern, encoding: Encoding.Default, delimiter: ",", firstLineContainsEncoding, failIfNotFound, multipleFiles: false, archiveIfSuccess);
    }

    /// <inheritdoc/>
    public async Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, int rowsToSkip, bool archiveIfSuccess = true)
    {
        return await ProcessFileAsync(basePath, fileNamePattern, encoding: Encoding.Default, delimiter: ",", firstLineContainsEncoding: false, failIfNotFound: true, multipleFiles: false, archiveIfSuccess, hardDelete: false, rowsToSkip);
    }

    /// <inheritdoc/>
    public async Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, int rowsToSkip, bool fixUnescapedQuotes, bool archiveIfSuccess = true)
    {
        return await ProcessFileAsync(basePath, fileNamePattern, encoding: Encoding.Default, delimiter: ",", firstLineContainsEncoding: false, failIfNotFound: true, multipleFiles: false, archiveIfSuccess, hardDelete: false, rowsToSkip, fixUnescapedQuotes);
    }

    private static string BuildArchivedFileName(string fileName, string timeStamp)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        return $"{name}_{timeStamp}{ext}";
    }

    private static string BuildArchivedBlobPath(string blobPath, string timeStamp)
    {
        var dir = Path.GetDirectoryName(blobPath)?.Replace('\\', '/');
        var name = Path.GetFileNameWithoutExtension(blobPath);
        var ext = Path.GetExtension(blobPath);
        var archiveName = $"{name}_{timeStamp}{ext}";
        return string.IsNullOrEmpty(dir) ? archiveName : $"{dir}/{archiveName}";
    }
}
