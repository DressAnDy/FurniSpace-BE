using Google.Cloud.Storage.V1;
using StorageObject = Google.Apis.Storage.v1.Data.Object;

namespace FurniSpace.Infrastructure.Common.Storage;

internal sealed class GoogleStorageObjectClient : IStorageObjectClient
{
    private readonly StorageClient _storageClient;

    public GoogleStorageObjectClient(StorageClient storageClient)
    {
        _storageClient = storageClient;
    }

    public Task<StorageObject> GetObjectAsync(
        string bucket,
        string objectName,
        CancellationToken cancellationToken = default) =>
        _storageClient.GetObjectAsync(bucket, objectName, cancellationToken: cancellationToken);

    public Task UpdateObjectAsync(
        StorageObject storageObject,
        CancellationToken cancellationToken = default) =>
        _storageClient.UpdateObjectAsync(storageObject, cancellationToken: cancellationToken);

    public Task UploadObjectAsync(
        StorageObject storageObject,
        Stream source,
        CancellationToken cancellationToken = default) =>
        _storageClient.UploadObjectAsync(storageObject, source, cancellationToken: cancellationToken);

    public Task DeleteObjectAsync(
        string bucket,
        string objectName,
        CancellationToken cancellationToken = default) =>
        _storageClient.DeleteObjectAsync(bucket, objectName, cancellationToken: cancellationToken);
}
