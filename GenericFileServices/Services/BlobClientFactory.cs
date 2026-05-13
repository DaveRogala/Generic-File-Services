using System.Collections.Concurrent;

namespace GenericFileServices.Services;

/// <summary>
/// Default implementation of <see cref="IBlobClientFactory"/>.
/// <see cref="BlobContainerClient"/> instances are cached by <c>(connectionString, containerName)</c>
/// key because this factory is registered as a Singleton and constructing a new
/// <see cref="BlobContainerClient"/> on every call would be wasteful.
/// </summary>
public class BlobClientFactory : IBlobClientFactory
{
    private readonly ConcurrentDictionary<(string connectionString, string containerName), BlobContainerClient> _containerClients = new();

    /// <inheritdoc/>
    public BlobClient GetBlobClient(string connectionString, string containerName, string blobPath) =>
        GetContainerClient(connectionString, containerName).GetBlobClient(blobPath);

    /// <inheritdoc/>
    public BlobContainerClient GetContainerClient(string connectionString, string containerName) =>
        _containerClients.GetOrAdd(
            (connectionString, containerName),
            static key => new BlobContainerClient(key.connectionString, key.containerName));
}
