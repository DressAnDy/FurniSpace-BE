namespace FurniSpace.Application.DTOs.ProjectChatMessages;

public sealed class ProjectChatMessageAttachmentDto
{
    public Guid FileId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string FileUrl { get; set; } = string.Empty;
}
