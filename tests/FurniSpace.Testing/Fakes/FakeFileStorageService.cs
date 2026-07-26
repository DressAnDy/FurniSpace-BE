using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.Interfaces;

namespace FurniSpace.Testing.Fakes;

public sealed class FakeFileStorageService : IFileStorageService
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

    public Task DeleteAsync(string objectName, CancellationToken cancellationToken = default)
    {
        DeletedObjectNames.Add(objectName);
        return Task.CompletedTask;
    }
}
