namespace GenericFileServices.Models;

/// <summary>
/// Holds the parsed rows and any parse errors produced from a single file,
/// along with the file's name for tracking and error reporting.
/// </summary>
/// <typeparam name="T">DTO type that each parsed row maps to.</typeparam>
public class FileResults<T> : ObjectResult<T>
{
    /// <summary>Initialises an empty result with an empty file name.</summary>
    [SetsRequiredMembers]
    public FileResults()
    {
        FileName = "";
    }

    /// <summary>Initialises with explicit file data and errors.</summary>
    /// <param name="fileName">Name of the source file.</param>
    /// <param name="fileData">Successfully parsed rows.</param>
    /// <param name="fileErrors">Per-row parse error messages.</param>
    [SetsRequiredMembers]
    public FileResults(string fileName, List<T> fileData, List<string> fileErrors)
        : base(fileData, fileErrors)
    {
        FileName = fileName;
    }

    /// <summary>Wraps an existing <see cref="ObjectResult{T}"/> with a file name.</summary>
    /// <param name="objectResult">Parse result from <c>MagellanFileServices</c>.</param>
    /// <param name="fileName">Name of the source file.</param>
    [SetsRequiredMembers]
    public FileResults(ObjectResult<T> objectResult, string fileName)
        : base(objectResult.ObjectResults, objectResult.Errors)
    {
        FileName = fileName;
    }

    /// <summary>Name of the file that produced this result.</summary>
    public required string FileName { get; set; }
}
