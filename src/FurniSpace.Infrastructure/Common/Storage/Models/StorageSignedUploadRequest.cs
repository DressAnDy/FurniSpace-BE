namespace FurniSpace.Infrastructure.Common.Storage;

public sealed class StorageSignedUploadRequest
{
    public string ObjectName { get; init; } = string.Empty;
    public string ContentType { get; init; } = "application/octet-stream";
    public TimeSpan Expiration { get; init; } = TimeSpan.FromMinutes(15);
}
