namespace GenericFileImportServices.Models;

public class FileResults<T> : ObjectResult<T>
{
    [SetsRequiredMembers]
    public FileResults()            
    {
        FileName = "";            
    }
    [SetsRequiredMembers]
    public FileResults(string fileName, List<T> fileData, List<string> fileErrors)
        : base (fileData, fileErrors)
    {
        FileName = fileName;           
    }
    [SetsRequiredMembers]
    public FileResults(ObjectResult<T> objectResult, string fileName)
        : base (objectResult.ObjectResults, objectResult.Errors)

    {
        FileName = fileName;
    }
    public required string FileName { get; set; }

}
