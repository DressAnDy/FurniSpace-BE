namespace FurniSpace.Infrastructure.ReadModels.ProjectChatMessages;

public sealed class ProjectChatMessageAttachmentReadModel
{
    public Guid FileId { get; init; }
    public string OriginalFileName { get; init; } = string.Empty;
    public string MimeType { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
    public string FileUrl { get; init; } = string.Empty;
}
