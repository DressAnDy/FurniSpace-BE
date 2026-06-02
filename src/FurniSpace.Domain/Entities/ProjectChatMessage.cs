using System;

namespace FurniSpace.Domain.Entities;

public class ProjectChatMessage
{
    public Guid MessageId { get; set; }
    public Guid ChatId { get; set; }
    public Guid? SenderId { get; set; }
    public string? MessageType { get; set; }
    public string? Content { get; set; }
    public Guid? AttachmentFileId { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? EditedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}


