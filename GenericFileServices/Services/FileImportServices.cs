using GenericFileServices.Models;

namespace GenericFileServices.Services;

/// <summary>
/// Abstract base class for file-to-database ETL import services.
/// Handles file discovery, parsing, database reconciliation, and file archiving or error reporting.
/// Consumers extend this class and implement <see cref="GetAddEntities"/>,
/// <see cref="GetUpdateEntities"/>, and <see cref="GetDeleteEntities"/> to define
/// how file rows map to database changes.
/// </summary>
/// <typeparam name="T">Entity type. Must extend <see cref="GenericFileServices.Models.Database.Base.BaseObject"/>.</typeparam>
/// <typeparam name="U">DTO type that each parsed file row maps to.</typeparam>
/// <typeparam name="C">EF Core <see cref="DbContext"/> type.</typeparam>
public abstract class FileImportServices<T, U, C> : IFileImportServices<T, U, C>
    where T : BaseObject
    where C : DbContext
{
    private readonly IDatabaseServices<T, C> _databaseServices;
    private readonly IFileReaderServices<U> _fileReaderServices;
    private readonly ILogger<FileImportServices<T, U, C>> _logger;

    private const string FileContainedErrors = "File contained errors";

    /// <summary>Initializes a new instance with the required collaborators.</summary>
    protected FileImportServices(
        IDatabaseServices<T, C> databaseServices,
        IFileReaderServices<U> fileReaderServices,
        ILogger<FileImportServices<T, U, C>> logger)
    {
        _fileReaderServices = fileReaderServices;
        _databaseServices = databaseServices;
        _logger = logger;
    }

    /// <inheritdoc cref="IFileImportServices{T, U, C}.ProcessFileAsync(Stream, string, string, string, Encoding, string, bool, bool, bool, bool, bool, int, bool)"/>
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

                string timeStamp = DateTime.UtcNow.ToString(Constants.TimestampFormat);
                try
                {
                    if (fileResult.ObjectResults is { Count: > 0 })
                    {
                        List<T> existingEntities = await _databaseServices.GetAllEntitiesAsync();

                        await _databaseServices.UpdateDatabaseAsync(
                            GetAddEntities(existingEntities, fileResult.ObjectResults),
                            GetUpdateEntities(existingEntities, fileResult.ObjectResults),
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

    /// <inheritdoc cref="IFileImportServices{T, U, C}.ProcessFileAsync(string, string, Encoding, string, bool, bool, bool, bool, bool, int, bool)"/>
    public virtual async Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, Encoding encoding, string delimiter = ",", bool firstLineContainsEncoding = false, bool failIfNotFound = true, bool multipleFiles = false, bool archiveIfSuccess = true, bool hardDelete = false, int rowsToSkip = 0, bool fixUnescapedQuotes = false)
    {
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(basePath, nameof(basePath));
            ArgumentException.ThrowIfNullOrWhiteSpace(fileNamePattern, nameof(fileNamePattern));
            var fileResults = _fileReaderServices.ReadFromFile(basePath, fileNamePattern, encoding, delimiter, firstLineContainsEncoding: firstLineContainsEncoding, failIfFileMissing: failIfNotFound, multipleFiles, rowsToSkip: rowsToSkip, fixUnescapedQuotes: fixUnescapedQuotes);
            List<string> errors = [..fileResults.SelectMany(f => f.Errors.Select(e => $"File: {f.FileName}: Error; {e}"))];

            if (fileResults.Count == 0) return errors;

            foreach (var fileResult in fileResults)
            {
                string timeStamp = DateTime.UtcNow.ToString(Constants.TimestampFormat);
                try
                {
                    if (fileResult.ObjectResults is { Count: > 0 })
                    {
                        // Reload before each file so subsequent files see entities added/updated by earlier files.
                        List<T> existingEntities = await _databaseServices.GetAllEntitiesAsync();
                        await _databaseServices.UpdateDatabaseAsync(
                            GetAddEntities(existingEntities, fileResult.ObjectResults),
                            GetUpdateEntities(existingEntities, fileResult.ObjectResults),
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

    /// <inheritdoc cref="IFileImportServices{T, U, C}.ProcessFileAsync(string, string, FileImportOptions)"/>
    public async Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, FileImportOptions options)
    {
        return await ProcessFileAsync(
            basePath, fileNamePattern,
            options.Encoding, options.Delimiter,
            options.FirstLineContainsEncoding, options.FailIfNotFound, options.MultipleFiles,
            options.ArchiveIfSuccess, options.HardDelete, options.RowsToSkip, options.FixUnescapedQuotes);
    }

    /// <inheritdoc cref="IFileImportServices{T, U, C}.ProcessFileAsync(Stream, string, string, string, FileImportOptions)"/>
    public async Task<List<string>> ProcessFileAsync(Stream stream, string blobConnectionString, string containerName, string filePath, FileImportOptions options)
    {
        return await ProcessFileAsync(
            stream, blobConnectionString, containerName, filePath,
            options.Encoding, options.Delimiter,
            options.FirstLineContainsEncoding, options.FailIfNotFound, options.MultipleFiles,
            options.ArchiveIfSuccess, options.HardDelete, options.RowsToSkip, options.FixUnescapedQuotes);
    }

    /// <inheritdoc cref="IFileImportServices{T, U, C}.ProcessFileAsync(string, string, Encoding, string, bool, bool, bool, bool, bool, int, bool)"/>
    public async Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, Encoding encoding, string delimiter = ",", bool failIfNotFound = true, bool archiveIfSuccess = true)
    {
        return await ProcessFileAsync(basePath, fileNamePattern, encoding, delimiter, firstLineContainsEncoding: false, failIfNotFound, multipleFiles: false, archiveIfSuccess);
    }

    /// <inheritdoc cref="IFileImportServices{T, U, C}.ProcessFileAsync(string, string, Encoding, string, bool, bool, bool, bool, bool, int, bool)"/>
    public async Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, Encoding encoding, string delimiter = ",", bool archiveIfSuccess = true)
    {
        return await ProcessFileAsync(basePath, fileNamePattern, encoding, delimiter, firstLineContainsEncoding: false, failIfNotFound: true, multipleFiles: false, archiveIfSuccess);
    }

    /// <inheritdoc cref="IFileImportServices{T, U, C}.ProcessFileAsync(string, string, Encoding, string, bool, bool, bool, bool, bool, int, bool)"/>
    public async Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, string delimiter = ",", bool failIfNotFound = true, bool multipleFiles = false, bool archiveIfSuccess = true)
    {
        return await ProcessFileAsync(basePath, fileNamePattern, encoding: Encoding.Default, delimiter, firstLineContainsEncoding: false, failIfNotFound, multipleFiles, archiveIfSuccess);
    }

    /// <inheritdoc cref="IFileImportServices{T, U, C}.ProcessFileAsync(string, string, Encoding, string, bool, bool, bool, bool, bool, int, bool)"/>
    public async Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, string delimiter = ",", bool failIfNotFound = true, bool archiveIfSuccess = true)
    {
        return await ProcessFileAsync(basePath, fileNamePattern, encoding: Encoding.Default, delimiter, firstLineContainsEncoding: false, failIfNotFound, multipleFiles: false, archiveIfSuccess);
    }

    /// <inheritdoc cref="IFileImportServices{T, U, C}.ProcessFileAsync(string, string, Encoding, string, bool, bool, bool, bool, bool, int, bool)"/>
    public async Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, string delimiter = ",", bool archiveIfSuccess = true)
    {
        return await ProcessFileAsync(basePath, fileNamePattern, encoding: Encoding.Default, delimiter, firstLineContainsEncoding: false, failIfNotFound: true, multipleFiles: false, archiveIfSuccess);
    }

    /// <inheritdoc cref="IFileImportServices{T, U, C}.ProcessFileAsync(string, string, Encoding, string, bool, bool, bool, bool, bool, int, bool)"/>
    public async Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, bool hardDelete)
    {
        return await ProcessFileAsync(basePath, fileNamePattern, encoding: Encoding.Default, delimiter: ",", firstLineContainsEncoding: false, failIfNotFound: true, multipleFiles: false, archiveIfSuccess: true, hardDelete);
    }

    /// <inheritdoc cref="IFileImportServices{T, U, C}.ProcessFileAsync(string, string, Encoding, string, bool, bool, bool, bool, bool, int, bool)"/>
    public async Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, bool firstLineContainsEncoding, bool hardDelete)
    {
        return await ProcessFileAsync(basePath, fileNamePattern, encoding: Encoding.Default, delimiter: ",", firstLineContainsEncoding, failIfNotFound: true, multipleFiles: false, archiveIfSuccess: true, hardDelete);
    }

    /// <inheritdoc cref="IFileImportServices{T, U, C}.ProcessFileAsync(string, string, Encoding, string, bool, bool, bool, bool, bool, int, bool)"/>
    public async Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, bool failIfNotFound, bool firstLineContainsEncoding, bool archiveIfSuccess)
    {
        return await ProcessFileAsync(basePath, fileNamePattern, encoding: Encoding.Default, delimiter: ",", firstLineContainsEncoding, failIfNotFound, multipleFiles: false, archiveIfSuccess);
    }

    /// <inheritdoc cref="IFileImportServices{T, U, C}.ProcessFileAsync(string, string, Encoding, string, bool, bool, bool, bool, bool, int, bool)"/>
    public async Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, int rowsToSkip, bool archiveIfSuccess = true)
    {
        return await ProcessFileAsync(basePath, fileNamePattern, encoding: Encoding.Default, delimiter: ",", firstLineContainsEncoding: false, failIfNotFound: true, multipleFiles: false, archiveIfSuccess, hardDelete: false, rowsToSkip);
    }

    /// <inheritdoc cref="IFileImportServices{T, U, C}.ProcessFileAsync(string, string, Encoding, string, bool, bool, bool, bool, bool, int, bool)"/>
    public async Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, int rowsToSkip, bool fixUnescapedQuotes, bool archiveIfSuccess = true)
    {
        return await ProcessFileAsync(basePath, fileNamePattern, encoding: Encoding.Default, delimiter: ",", firstLineContainsEncoding: false, failIfNotFound: true, multipleFiles: false, archiveIfSuccess, hardDelete: false, rowsToSkip, fixUnescapedQuotes);
    }
}
