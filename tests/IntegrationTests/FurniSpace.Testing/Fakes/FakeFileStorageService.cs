using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.Interfaces;

namespace FurniSpace.Testing.Fakes;

public sealed class FakeFileStorageService : IFileStorageService, IDirectFileUploadStorageService
{
    public List<string> DeletedObjectNames { get; } = [];

    public Task<StorageUploadResult> UploadAsync(
        StorageUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        var objectName = $"integration/{Guid.NewGuid():N}";
        return Task.FromResult(new StorageUploadResult
        {
            ObjectName = objectName,
            PublicUrl = $"https://storage.integration.test/{objectName}",
            Bucket = "furnispace-integration"
        });
    }

    public Task<StorageSignedUploadResult> CreateSignedUploadUrlAsync(
        StorageSignedUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new StorageSignedUploadResult
        {
            UploadUrl = $"https://storage.integration.test/upload/{request.ObjectName}",
            ObjectName = request.ObjectName,
            Bucket = "furnispace-integration",
            ContentType = request.ContentType,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15)
        });
    }

    public Task<StorageUploadResult> FinalizeDirectUploadAsync(
        StorageDirectUploadFinalizeRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new StorageUploadResult
        {
            ObjectName = request.ObjectName,
            PublicUrl = $"https://storage.integration.test/{request.ObjectName}",
            Bucket = "furnispace-integration"
        });
    }

    public Task DeleteAsync(string objectName, CancellationToken cancellationToken = default)
    {
        DeletedObjectNames.Add(objectName);
        return Task.CompletedTask;
    }
}
