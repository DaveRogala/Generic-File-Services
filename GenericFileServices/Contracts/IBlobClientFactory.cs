namespace GenericFileServices.Contracts;

/// <summary>
/// Creates <see cref="BlobClient"/> instances. Abstracted to allow test doubles
/// in unit tests without requiring a live Azure Storage endpoint.
/// </summary>
public interface IBlobClientFactory
{
    /// <summary>
    /// Returns a <see cref="BlobClient"/> for the specified blob.
    /// </summary>
    BlobClient GetBlobClient(string connectionString, string containerName, string blobPath);
}
