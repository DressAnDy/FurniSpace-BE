namespace FurniSpace.Infrastructure.Common.Storage;

public sealed class StorageSignedUploadResult
{
    public string UploadUrl { get; init; } = string.Empty;
    public string ObjectName { get; init; } = string.Empty;
    public string Bucket { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
}
