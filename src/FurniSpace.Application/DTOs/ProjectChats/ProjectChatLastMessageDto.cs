namespace FurniSpace.Application.DTOs.ProjectChats;

public sealed class ProjectChatLastMessageDto
{
    public Guid MessageId { get; set; }
    public Guid? SenderId { get; set; }
    public string? SenderName { get; set; }
    public string MessageType { get; set; } = string.Empty;
    public string? ContentPreview { get; set; }
    public DateTime? CreatedAt { get; set; }
}
