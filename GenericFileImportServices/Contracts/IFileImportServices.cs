namespace GenericFileImportServices.Contracts;

public interface IFileImportServices<T, U, C>
    where T : BaseObject
    where C : DbContext
{
    Task<List<string>> ProcessFileAsync(Stream stream, string blobConnectionString, string containerName, string filePath, Encoding encoding, string delimiter = ",", bool firstLineContainsEncoding = false, bool failIfNotFound = true, bool multipleFiles = false, bool archiveIfSuccess = true, bool hardDelete = false, int rowsToSkip = 0, bool fixUnescapedQuotes = false);
    Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, Encoding encoding, string delimiter = ",", bool firstLineContainsEncoding = false, bool failIfNotFound = true, bool multipleFiles = false, bool archiveIfSuccess = true, bool hardDelete = false, int rowsToSkip = 0, bool fixUnescapedQuotes = false);
    Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, int rowsToSkip, bool archiveIfSuccess = true);
    Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, int rowsToSkip, bool fixUnescapedQuotes, bool archiveIfSuccess = true);
    Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, Encoding encoding, string delimiter = ",", bool failIfNotFound = true, bool archiveIfSuccess = true);
    Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, Encoding encoding, string delimiter = ",", bool archiveIfSuccess = true);
    Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, string delimiter = ",", bool failIfNotFound = true, bool multipleFiles = false, bool archiveIfSuccess = true);
    Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, string delimiter = ",", bool failIfNotFound = true, bool archiveIfSuccess = true);
    Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, string delimiter = ",", bool archiveIfSuccess = true);
    Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, bool hardDelete);
    Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern, bool firstLineContainsEncoding, bool hardDelete);
    Task<List<string>> ProcessFileAsync(string basePath, string fileNamePattern,  bool failIfNotFound, bool firstLineContainsEncoding, bool archiveIfSuccess);
    
}
