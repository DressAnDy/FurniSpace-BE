using StorageObject = Google.Apis.Storage.v1.Data.Object;

namespace FurniSpace.Infrastructure.Common.Storage;

public interface IStorageObjectClient
{
    Task<StorageObject> GetObjectAsync(
        string bucket,
        string objectName,
        CancellationToken cancellationToken = default);

    Task UpdateObjectAsync(
        StorageObject storageObject,
        CancellationToken cancellationToken = default);

    Task UploadObjectAsync(
        StorageObject storageObject,
        Stream source,
        CancellationToken cancellationToken = default);

    Task DeleteObjectAsync(
        string bucket,
        string objectName,
        CancellationToken cancellationToken = default);
}
