namespace FurniSpace.Infrastructure.Common.Storage;

public sealed class StorageUploadResult
{
    public string ObjectName { get; init; } = string.Empty;
    public string PublicUrl { get; init; } = string.Empty;
    public string Bucket { get; init; } = string.Empty;
}
