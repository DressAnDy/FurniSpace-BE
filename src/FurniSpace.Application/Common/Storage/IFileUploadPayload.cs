namespace FurniSpace.Application.Common.Storage;

public interface IFileUploadPayload
{
    Stream Content { get; }
    string OriginalFileName { get; }
    string ContentType { get; }
    long FileSizeBytes { get; }
}
