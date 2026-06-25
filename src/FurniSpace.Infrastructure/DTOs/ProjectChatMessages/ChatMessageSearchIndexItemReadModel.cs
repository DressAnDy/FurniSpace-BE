using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.DTOs.ProjectChatMessages;

public sealed class ChatMessageSearchIndexItemReadModel
{
    public Guid MessageId { get; set; }
    public Guid ChatId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? SenderId { get; set; }
    public string? SenderName { get; set; }
    public ProjectChatMessageType? MessageType { get; set; }
    public string? Content { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
