
namespace GenericFileImportServices.Services;

public class FileReaderServices<U>  : IFileReaderServices<U>

{ 
    private protected readonly IFileServices _fileServices;
    private protected readonly ILogger<FileReaderServices<U>> _logger;
    
    public FileReaderServices(IFileServices fileServices, ILogger<FileReaderServices<U>> logger)

    {
        _fileServices = fileServices;
        _logger = logger;
    }    
    
    public void HandleFileError(string basePath, string fileName, string exceptionMessage, string timeStamp, List<string>? errors = null)
    {
        _fileServices.HandleFileError(basePath, fileName, exceptionMessage, timeStamp, errors);
    }

    public void HandleFileSuccess(string basePath, string fileName, string timeStamp)
    {
        _fileServices.HandleFileSuccess(basePath, fileName, timeStamp);
    }

    public async Task HandleFileSuccessAsync(Stream stream, string blobConnectionString, string containerName, string filePath, string timeStamp)
    {
        await _fileServices.HandleFileSuccessAsync(stream, blobConnectionString, containerName, filePath, timeStamp);
    }

    public async Task HandleFileErrorAsync(Stream stream, string blobConnectionString, string containerName, string filePath, string exceptionMessage, string timeStamp, List<string>? errors = null)
    {
        await _fileServices.HandlFileErrorAsync(stream,blobConnectionString,containerName,filePath, exceptionMessage, timeStamp, errors);
    }

    public virtual List<FileResults<U>> ReadFromFile(string basePath, string fileNamePattern, Encoding encoding, string delimiter = ",", bool firstLineContainsEncoding = false, bool failIfFileMissing = true, bool multipleFiles = false, int rowsToSkip = 0, bool fixUnescapedQuotes = false)
    {
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(basePath, nameof(basePath));
            ArgumentException.ThrowIfNullOrWhiteSpace(fileNamePattern);
            DirectoryInfo di = new(basePath);
            List<FileInfo> files = di.GetFiles(fileNamePattern).ToList();
            List<FileResults<U>> fileResults = [];

            int fileCount = files.Count;

            _logger.LogInformation("Found {fileCount} files matching pattern {fileNamePattern}", fileCount, fileNamePattern);

            if (failIfFileMissing && fileCount == 0)
            {
                throw new Exception("No file found");
            }
            if (!multipleFiles && files.Count > 1)
            {
                throw new Exception($"{fileCount} found matching pattern {fileNamePattern}");
            }

            foreach (FileInfo file in files.OrderBy(f => f.LastWriteTime))
            {
                _logger.LogInformation("Processing file {fileName}", file.Name);

                ObjectResult<U> importResult = _fileServices.GetDataFromFile<U>(Path.Combine(basePath, file.Name), encoding, firstLineContainsEncoding, delimiter, rowsToSkip, fixUnescapedQuotes);

                fileResults.Add(new(importResult, file.Name));
                if (importResult.Errors.Count > 0)
                {
                    _logger.LogWarning("File {fileName} import completed with errors", file.FullName);
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


    public List<FileResults<U>> ReadFromFile(string basePath, string fileNamePattern, bool firstLineContainsEncoding, bool failIfFileMissing = true)
    {
        return ReadFromFile(basePath, fileNamePattern, encoding: Encoding.Default, delimiter: ",", firstLineContainsEncoding, failIfFileMissing, multipleFiles: false);
    }

    public List<FileResults<U>> ReadFromFile(Stream stream, string fileName, Encoding encoding, bool firstLineContainsEncoding, string delimiter = ",", int rowsToSkip = 0, bool fixUnescapedQuotes = false)
    {
        try
        {
            List<FileResults<U>> fileResults = [];
            ObjectResult<U> importResult = _fileServices.GetDataFromFile<U>(stream, encoding, firstLineContainsEncoding, delimiter, rowsToSkip, fixUnescapedQuotes);

            return [new(importResult,fileName)];

            

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading from file");
            throw;
        }
    }
}
