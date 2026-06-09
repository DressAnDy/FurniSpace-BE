namespace FurniSpace.Infrastructure.Storage;

public sealed class StorageUploadRequest
{
    public Stream Content { get; init; } = Stream.Null;
    public string ObjectName { get; init; } = string.Empty;
    public string ContentType { get; init; } = "application/octet-stream";
}
