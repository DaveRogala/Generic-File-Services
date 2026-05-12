namespace GenericFileServices.Services;

/// <summary>
/// Default implementation of <see cref="IBlobClientFactory"/>.
/// Constructs a <see cref="BlobContainerClient"/> and returns its <see cref="BlobClient"/>.
/// </summary>
public class BlobClientFactory : IBlobClientFactory
{
    public BlobClient GetBlobClient(string connectionString, string containerName, string blobPath) =>
        new BlobContainerClient(connectionString, containerName).GetBlobClient(blobPath);
}
