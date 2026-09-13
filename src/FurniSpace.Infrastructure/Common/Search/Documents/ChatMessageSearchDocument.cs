namespace FurniSpace.Infrastructure.Common.Search.Documents;

public sealed class ChatMessageSearchDocument
{
    public Guid MessageId { get; set; }
    public Guid ChatId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? SenderId { get; set; }
    public string? SenderName { get; set; }
    public string? MessageType { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
}
