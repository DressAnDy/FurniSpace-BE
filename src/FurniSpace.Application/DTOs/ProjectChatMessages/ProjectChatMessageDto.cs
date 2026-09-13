namespace FurniSpace.Application.DTOs.ProjectChatMessages;

public sealed class ProjectChatMessageDto
{
    public Guid MessageId { get; set; }
    public Guid ChatId { get; set; }
    public Guid? SenderId { get; set; }
    public string? SenderName { get; set; }
    public string? SenderRole { get; set; }
    public string MessageType { get; set; } = string.Empty;
    public string? Content { get; set; }
    public ProjectChatMessageAttachmentDto? Attachment { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? EditedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}
