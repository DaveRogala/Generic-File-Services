namespace GenericFileServices.Services;

/// <summary>
/// Default implementation of <see cref="IFileWriterServices"/>.
/// Writes delimited content to a local/network path or an Azure Blob Storage container.
/// Fields are quoted (RFC 4180) when they contain the delimiter, a double-quote, or a line terminator.
/// </summary>
public class FileWriterServices(ILogger<FileWriterServices> logger) : IFileWriterServices
{
    private readonly ILogger<FileWriterServices> _logger = logger;

    /// <inheritdoc/>
    public void WriteToFile(
        string basePath,
        string fileName,
        IEnumerable<string> headers,
        IEnumerable<IEnumerable<string>> rows,
        Encoding encoding,
        string delimiter = ",")
    {
        var path = Path.Combine(basePath, fileName);
        _logger.LogInformation("Writing export file {FileName} to {BasePath}", fileName, basePath);
        using var writer = new StreamWriter(path, append: false, encoding);
        WriteContent(writer, headers, rows, delimiter);
    }

    /// <inheritdoc/>
    public async Task WriteToBlobAsync(
        string blobConnectionString,
        string containerName,
        string blobPath,
        IEnumerable<string> headers,
        IEnumerable<IEnumerable<string>> rows,
        Encoding encoding,
        string delimiter = ",")
    {
        _logger.LogInformation("Writing export blob {BlobPath} to container {ContainerName}", blobPath, containerName);
        var containerClient = new BlobContainerClient(blobConnectionString, containerName);
        var blobClient = containerClient.GetBlobClient(blobPath);

        using var stream = new MemoryStream();
        await using (var writer = new StreamWriter(stream, encoding, leaveOpen: true))
        {
            WriteContent(writer, headers, rows, delimiter);
        }
        stream.Position = 0;
        await blobClient.UploadAsync(stream, overwrite: true);
    }

    private static readonly string TimestampFormat = "yyyyMMddHHmmssfff";

    /// <inheritdoc/>
    public void ArchiveExistingFile(string basePath, string fileName, string? archivePath = null)
    {
        var sourcePath = Path.Combine(basePath, fileName);
        if (!File.Exists(sourcePath))
            return;

        var resolvedArchivePath = archivePath is null
            ? Path.Combine(basePath, "archive")
            : Path.IsPathRooted(archivePath)
                ? archivePath
                : Path.Combine(basePath, archivePath);

        Directory.CreateDirectory(resolvedArchivePath);

        var timeStamp = DateTime.UtcNow.ToString(TimestampFormat);
        var name = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        var destPath = Path.Combine(resolvedArchivePath, $"{name}_{timeStamp}{ext}");

        File.Move(sourcePath, destPath);
        _logger.LogInformation("Archived {FileName} to {DestPath}", fileName, destPath);
    }

    private static void WriteContent(
        StreamWriter writer,
        IEnumerable<string> headers,
        IEnumerable<IEnumerable<string>> rows,
        string delimiter)
    {
        var headerList = headers.ToList();
        if (headerList.Count > 0)
            writer.WriteLine(FormatRow(headerList, delimiter));

        foreach (var row in rows)
            writer.WriteLine(FormatRow(row, delimiter));
    }

    private static string FormatRow(IEnumerable<string> fields, string delimiter) =>
        string.Join(delimiter, fields.Select(f => QuoteField(f, delimiter)));

    private static string QuoteField(string field, string delimiter)
    {
        if (!field.Contains(delimiter) && !field.Contains('"') &&
            !field.Contains('\n') && !field.Contains('\r'))
            return field;
        return $"\"{field.Replace("\"", "\"\"")}\"";
    }
}
