using FurniSpace.Application.Common.Storage;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.ProjectChatMessages;

public sealed class SendFileChatMessageRequestDto : IFileUploadPayload
{
    public Stream FileContent { get; init; } = Stream.Null;
    public string OriginalFileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = "application/octet-stream";
    public long FileSizeBytes { get; init; }
    public FileType FileType { get; init; } = FileType.OTHER;
    public FileVisibility? Visibility { get; init; }
    public string? Content { get; init; }

    Stream IFileUploadPayload.Content => FileContent;
}
