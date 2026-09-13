using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.ProjectChatMessages;

public sealed class PrepareProjectChatFileUploadRequestDto
{
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long FileSizeBytes { get; set; }
    public FileType FileType { get; set; } = FileType.OTHER;
    public FileVisibility? Visibility { get; set; }
}

public sealed class PrepareProjectChatFileUploadResponseDto
{
    public Guid FileId { get; set; }
    public Guid ChatId { get; set; }
    public Guid ProjectId { get; set; }
    public string UploadUrl { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

public sealed class CompleteProjectChatFileUploadRequestDto
{
    public Guid FileId { get; set; }
    public string? Content { get; set; }
}
