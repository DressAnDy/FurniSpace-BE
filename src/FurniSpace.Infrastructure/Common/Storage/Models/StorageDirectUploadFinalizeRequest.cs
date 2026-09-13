namespace FurniSpace.Infrastructure.Common.Storage;

public sealed class StorageDirectUploadFinalizeRequest
{
    public string ObjectName { get; init; } = string.Empty;
    public string ContentType { get; init; } = "application/octet-stream";
    public long ExpectedSizeBytes { get; init; }
}
