namespace GenericFileServices.Services;

/// <summary>
/// Default implementation of <see cref="IFileReaderServices{U}"/>.
/// Delegates file I/O and parsing to <c>MagellanFileServices.IFileServices</c>,
/// adding structured logging and a typed <see cref="FileResults{U}"/> wrapper.
/// Override <see cref="ReadFromFile(string,string,Encoding,string,bool,bool,bool,int,bool)"/>
/// to supply custom parsing logic (e.g. fixed-width or multi-section files).
/// </summary>
/// <typeparam name="U">DTO type that each parsed row maps to.</typeparam>
public class FileReaderServices<U> : IFileReaderServices<U>
{
    private readonly IFileServices _fileServices;
    private readonly ILogger<FileReaderServices<U>> _logger;
    private readonly IBlobClientFactory _blobClientFactory;

    /// <summary>Initializes a new instance with the required collaborators.</summary>
    public FileReaderServices(IFileServices fileServices, ILogger<FileReaderServices<U>> logger, IBlobClientFactory blobClientFactory)
    {
        _fileServices = fileServices;
        _logger = logger;
        _blobClientFactory = blobClientFactory;
    }

    /// <inheritdoc/>
    public void HandleFileError(string basePath, string fileName, string exceptionMessage, string timeStamp, List<string>? errors = null)
    {
        _fileServices.HandleFileError(basePath, fileName, exceptionMessage, timeStamp, errors);
    }

    /// <inheritdoc/>
    public void HandleFileSuccess(string basePath, string fileName, string timeStamp)
    {
        _fileServices.HandleFileSuccess(basePath, fileName, timeStamp);
    }

    /// <inheritdoc/>
    public async Task HandleFileSuccessAsync(Stream stream, string blobConnectionString, string containerName, string filePath, string timeStamp, CancellationToken cancellationToken = default)
    {
        var containerClient = _blobClientFactory.GetContainerClient(blobConnectionString, containerName);
        await _fileServices.HandleFileSuccessAsync(containerClient, filePath, timeStamp);
    }

    /// <inheritdoc/>
    public async Task HandleFileErrorAsync(Stream stream, string blobConnectionString, string containerName, string filePath, string exceptionMessage, string timeStamp, List<string>? errors = null, CancellationToken cancellationToken = default)
    {
        var containerClient = _blobClientFactory.GetContainerClient(blobConnectionString, containerName);
        await _fileServices.HandleFileErrorAsync(containerClient, filePath, exceptionMessage, timeStamp, errors);
    }

    /// <inheritdoc/>
    public virtual List<FileResults<U>> ReadFromFile(string basePath, string fileNamePattern, Encoding encoding, string delimiter = ",", bool firstLineContainsEncoding = false, bool failIfFileMissing = true, bool multipleFiles = false, int rowsToSkip = 0, bool fixUnescapedQuotes = false, bool fileHasHeader = true)
    {
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(basePath, nameof(basePath));
            ArgumentException.ThrowIfNullOrWhiteSpace(fileNamePattern, nameof(fileNamePattern));
            DirectoryInfo di = new(basePath);
            FileInfo[] files = di.GetFiles(fileNamePattern);
            List<FileResults<U>> fileResults = [];

            int fileCount = files.Length;

            _logger.LogInformation("Found {fileCount} files matching pattern {fileNamePattern}", fileCount, fileNamePattern);

            if (failIfFileMissing && fileCount == 0)
            {
                throw new FileNotFoundException("No matching file found");
            }
            if (!multipleFiles && fileCount > 1)
            {
                throw new InvalidOperationException($"{fileCount} files found matching pattern {fileNamePattern}");
            }

            foreach (FileInfo file in files.OrderBy(f => f.LastWriteTime))
            {
                _logger.LogInformation("Processing file {fileName}", file.Name);

                var filePath = Path.Combine(basePath, file.Name);
                ObjectResult<U> importResult = !fileHasHeader
                    ? ReadHeaderless(filePath, encoding, delimiter, rowsToSkip)
                    : rowsToSkip > 0 || fixUnescapedQuotes
                        ? _fileServices.GetDataFromFile<U>(filePath, encoding, rowsToSkip, delimiter, fixUnescapedQuotes)
                        : _fileServices.GetDataFromFile<U>(filePath, encoding, firstLineContainsEncoding, delimiter);

                fileResults.Add(new(importResult, file.Name));
                if (importResult.Errors.Count > 0)
                {
                    _logger.LogWarning("File {filePath} import completed with errors", filePath);
                }
            }
            return fileResults;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading from file");
            throw;
        }
    }

    /// <inheritdoc/>
    public List<FileResults<U>> ReadFromFile(string basePath, string fileNamePattern, bool firstLineContainsEncoding, bool failIfFileMissing = true)
    {
        return ReadFromFile(basePath, fileNamePattern, encoding: Encoding.Default, delimiter: ",", firstLineContainsEncoding, failIfFileMissing, multipleFiles: false);
    }

    /// <inheritdoc/>
    public List<FileResults<U>> ReadFromFile(Stream stream, string fileName, Encoding encoding, bool firstLineContainsEncoding, string delimiter = ",", int rowsToSkip = 0, bool fixUnescapedQuotes = false, bool fileHasHeader = true)
    {
        try
        {
            ObjectResult<U> importResult = !fileHasHeader
                ? ReadHeaderless(stream, encoding, delimiter, rowsToSkip)
                : rowsToSkip > 0 || fixUnescapedQuotes
                    ? _fileServices.GetDataFromFile<U>(stream, encoding, rowsToSkip, delimiter, fixUnescapedQuotes)
                    : _fileServices.GetDataFromFile<U>(stream, encoding, firstLineContainsEncoding, delimiter);
            return [new(importResult, fileName)];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading from file");
            throw;
        }
    }

    private static ObjectResult<U> ReadHeaderless(string filePath, Encoding encoding, string delimiter, int rowsToSkip)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false,
            Delimiter = delimiter,
        };
        var records = new List<U>();
        var errors = new List<string>();
        using var reader = new StreamReader(filePath, encoding);
        using var csv = new CsvReader(reader, config);
        for (int i = 0; i < rowsToSkip; i++)
            csv.Read();
        while (csv.Read())
        {
            try { records.Add(csv.GetRecord<U>()!); }
            catch (Exception ex) { errors.Add(ex.Message); }
        }
        return new(records, errors);
    }

    private static ObjectResult<U> ReadHeaderless(Stream stream, Encoding encoding, string delimiter, int rowsToSkip)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false,
            Delimiter = delimiter,
        };
        var records = new List<U>();
        var errors = new List<string>();
        using var reader = new StreamReader(stream, encoding, leaveOpen: true);
        using var csv = new CsvReader(reader, config);
        for (int i = 0; i < rowsToSkip; i++)
            csv.Read();
        while (csv.Read())
        {
            try { records.Add(csv.GetRecord<U>()!); }
            catch (Exception ex) { errors.Add(ex.Message); }
        }
        return new(records, errors);
    }
}
