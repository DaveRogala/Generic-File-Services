
using GenericFileImportServices.Models;
using System.Runtime.CompilerServices;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace GenericFileImportServices.Services;

public abstract class FileImportServices<T, U, C> : IFileImportServices<T, U, C>
    where T : BaseObject
    where C : DbContext
{
    internal readonly IDatabaseServices<T, C> _databaseServices;
    internal readonly IFileReaderServices<U> _fileReaderServices;
    internal readonly ILogger<FileImportServices<T, U, C>> _logger;        


    public FileImportServices( IDatabaseServices<T,C> databaseServices,
                              IFileReaderServices<U> fileReaderServices,
                              ILogger<FileImportServices<T, U, C>> logger   
                              )
    {
        _fileReaderServices = fileReaderServices;
        _databaseServices = databaseServices;
        _logger = logger;
    }
    public async Task<List<string>> ProcessFileAsync(Stream stream, string blobConnectionString, string containerName, string filePath, Encoding encoding, string delimiter = ",", bool firstLineContainsEncoding = false, bool failIfNotFound = true, bool multipleFiles = false, bool archiveIfSuccess = true, bool hardDelete = false)
    {
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(blobConnectionString, nameof(blobConnectionString));
            ArgumentException.ThrowIfNullOrWhiteSpace(containerName, nameof(containerName));
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath, nameof(filePath));

            FileResults<U>? fileResult = _fileReaderServices.ReadFromFile(stream,Path.GetFileName(filePath), encoding, firstLineContainsEncoding, delimiter).FirstOrDefault();
            
            if (fileResult is not null)
            {
                List<string> errors = fileResult.Errors;          

                string timeStamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssffff");
                try
                {
                    if (fileResult.ObjectResults is not null)
                    {
                        List<T> existingEntities = await _databaseServices.GetAllEntitiesAsync();

                        await _databaseServices.UpdateDatabaseAsync(existingEntities,
                                                                    GetAddEntities(existingEntities, fileResult.ObjectResults),
                                                                    GetUpdateEntities(existingEntities, fileResult.ObjectResults),
                                                                    GetDeleteEntities(existingEntities, fileResult.ObjectResults),
                                                                    hardDelete);
                    }
                    if (fileResult.Errors.Count > 0)
                    {
                        await _fileReaderServices.HandleFileErrorAsync(stream, blobConnectionString,containerName,filePath, "File contained errors", timeStamp, fileResult.Errors);
                        
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
            throw new Exception("Unable to read from file");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            throw;
        }

    }
    
    public virtual async Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, Encoding encoding, string delimiter = ",", bool firstLineContainsEncoding = false, bool failIfNotFound = true, bool multipleFiles = false, bool archiveIfSuccess = true, bool hardDelete = false)
    {
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(basePath, nameof(basePath));
            ArgumentException.ThrowIfNullOrWhiteSpace(fileNamePattern);
            //TODO account for wanting entire file to fail if line errors
            var fileResults = _fileReaderServices.ReadFromFile(basePath, fileNamePattern,encoding,delimiter,firstLineContainsEncoding:firstLineContainsEncoding,failIfFileMissing: failIfNotFound, multipleFiles);
            List<string> errors = [..fileResults.SelectMany(f => f.Errors.Select(e => $"File: {f.FileName}: Error; {e}"))];

            foreach (var fileResult in fileResults) 
            {
                string timeStamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssffff");
                try
                {
                    if(fileResult.ObjectResults is not null)
                    {
                        List<T> existingEntities = await _databaseServices.GetAllEntitiesAsync();

                        await _databaseServices.UpdateDatabaseAsync(existingEntities, 
                                                                    GetAddEntities(existingEntities, fileResult.ObjectResults),
                                                                    GetUpdateEntities(existingEntities,fileResult.ObjectResults), 
                                                                    GetDeleteEntities(existingEntities, fileResult.ObjectResults),
                                                                    hardDelete);                        
                    }
                    if (fileResult.Errors.Count > 0)
                    {
                        _fileReaderServices.HandleFileError(basePath, fileResult.FileName, "File contained errors", timeStamp, fileResult.Errors);
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
            _logger.LogError(ex, ex.Message);
            throw;
        }
    }        
    public abstract List<T> GetDeleteEntities(List<T> existingEntities, List<U> dtos);       
    public abstract List<T> GetUpdateEntities(List<T> existingEntities, List<U> dtos);
    public abstract List<T> GetAddEntities(List<T> existingEntities, List<U> dtos);
    
    public async Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, Encoding encoding, string delimiter = ",", bool failIfNotFound = true, bool archiveIfSuccess = true)
    {
        return await ProcessFileAsync(basePath, fileNamePattern, encoding,delimiter, firstLineContainsEncoding: false, failIfNotFound, multipleFiles: false, archiveIfSuccess);
    }
    public async Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, Encoding encoding, string delimiter = ",", bool archiveIfSuccess = true)
    {
        return await ProcessFileAsync(basePath,fileNamePattern, encoding,delimiter, firstLineContainsEncoding: false,failIfNotFound:true,multipleFiles:false, archiveIfSuccess);
    }
    public async Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, string delimiter = ",", bool failIfNotFound = true, bool multipleFiles = false, bool archiveIfSuccess = true)
    {
        return await ProcessFileAsync(basePath,fileNamePattern,encoding:Encoding.Default,delimiter, firstLineContainsEncoding:false, failIfNotFound, multipleFiles, archiveIfSuccess);
    }
    public async Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, string delimiter = ",", bool failIfNotFound = true, bool archiveIfSuccess = true)
    {
        return await ProcessFileAsync(basePath,fileNamePattern,encoding:Encoding.Default,delimiter, firstLineContainsEncoding:false,failIfNotFound, multipleFiles:false,archiveIfSuccess);
    }
    public async Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, string delimiter = ",", bool archiveIfSuccess = true)
    {
        return await ProcessFileAsync(basePath, fileNamePattern, encoding: Encoding.Default,delimiter, firstLineContainsEncoding: false, failIfNotFound: true, multipleFiles: false, archiveIfSuccess);
    }
    public async Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, bool hardDelete)
    {
        return await ProcessFileAsync(basePath, fileNamePattern, encoding: Encoding.Default, delimiter: ",", firstLineContainsEncoding: false, failIfNotFound: true, multipleFiles: false, archiveIfSuccess:true,hardDelete);
    }
    public async Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, bool firstLineContainsEncoding, bool hardDelete)
    {
        return await ProcessFileAsync(basePath, fileNamePattern, encoding: Encoding.Default, delimiter: ",", firstLineContainsEncoding, failIfNotFound: true, multipleFiles: false, archiveIfSuccess: true,hardDelete);
    }
    public async Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, bool failIfNotFound, bool firstLineContainsEncoding, bool archiveIfSuccess)
    {
        return await ProcessFileAsync(basePath, fileNamePattern, encoding: Encoding.Default, delimiter: ",", firstLineContainsEncoding, failIfNotFound);
    }

    
}
