using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.ProjectChatMessages;

public sealed class ProjectChatMessageReadModel
{
    public Guid MessageId { get; init; }
    public Guid ChatId { get; init; }
    public Guid? SenderId { get; init; }
    public string? SenderName { get; init; }
    public string? SenderRole { get; init; }
    public ProjectChatMessageType? MessageType { get; init; }
    public string? Content { get; init; }
    public ProjectChatMessageAttachmentReadModel? Attachment { get; init; }
    public DateTime? CreatedAt { get; init; }
    public DateTime? EditedAt { get; init; }
    public DateTime? DeletedAt { get; init; }
    public DateTime? ReadAt { get; init; }
}
