using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.DTOs.ProjectChats;

public sealed class ProjectChatLastMessageReadModel
{
    public Guid MessageId { get; set; }
    public Guid? SenderId { get; set; }
    public string? SenderName { get; set; }
    public ProjectChatMessageType? MessageType { get; set; }
    public string? ContentPreview { get; set; }
    public DateTime? CreatedAt { get; set; }
}
