namespace GenericFileImportServices.Contracts;

public interface IFileReaderServices<U>
{
    List<FileResults<U>> ReadFromFile(string basePath, string fileNamePattern, Encoding encoding, string delimiter = ",", bool firstLineContainsEncoding = false, bool failIfFileMissing = true, bool multipleFiles = false, int rowsToSkip = 0, bool fixUnescapedQuotes = false);
    List<FileResults<U>> ReadFromFile(string basePath, string fileNamePattern, bool firstLineContainsEncoding, bool failIfFileMissing = true);
    List<FileResults<U>> ReadFromFile(Stream stream, string fileName, Encoding encoding, bool firstLineContainsEncoding, string delimiter = ",", int rowsToSkip = 0, bool fixUnescapedQuotes = false);


    void HandleFileError(string basePath, string fileName, string exceptionMessage, string timeStamp, List<string>? errors = null);
    Task HandleFileErrorAsync(Stream stream, string blobConnectionString, string containerName, string filePath, string exceptionMessage, string timeStamp, List<string>? errors = null);
    void HandleFileSuccess(string basePath, string fileName, string timeStamp);
    Task HandleFileSuccessAsync(Stream stream, string blobConnectionString, string containerName, string filePath, string timeStamp);
}
