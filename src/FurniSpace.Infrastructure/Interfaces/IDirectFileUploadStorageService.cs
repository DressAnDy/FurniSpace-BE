using FurniSpace.Infrastructure.Common.Storage;

namespace FurniSpace.Infrastructure.Interfaces;

public interface IDirectFileUploadStorageService
{
    Task<StorageSignedUploadResult> CreateSignedUploadUrlAsync(
        StorageSignedUploadRequest request,
        CancellationToken cancellationToken = default);

    Task<StorageUploadResult> FinalizeDirectUploadAsync(
        StorageDirectUploadFinalizeRequest request,
        CancellationToken cancellationToken = default);
}
