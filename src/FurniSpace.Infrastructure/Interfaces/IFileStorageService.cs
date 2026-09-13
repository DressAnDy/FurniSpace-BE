using FurniSpace.Infrastructure.Common.Storage;

namespace FurniSpace.Infrastructure.Interfaces;

public interface IFileStorageService
{
    Task<StorageUploadResult> UploadAsync(
        StorageUploadRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string objectName,
        CancellationToken cancellationToken = default);
}
