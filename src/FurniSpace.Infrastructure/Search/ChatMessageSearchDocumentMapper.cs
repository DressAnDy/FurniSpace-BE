using FurniSpace.Infrastructure.Common.Search.Documents;
using FurniSpace.Infrastructure.DTOs.ProjectChatMessages;

namespace FurniSpace.Infrastructure.Search;

public static class ChatMessageSearchDocumentMapper
{
    public static bool IsIndexable(ChatMessageSearchIndexItemReadModel item)
    {
        return item.DeletedAt is null &&
            !string.IsNullOrWhiteSpace(item.Content);
    }

    public static ChatMessageSearchDocument ToDocument(ChatMessageSearchIndexItemReadModel item)
    {
        return new ChatMessageSearchDocument
        {
            MessageId = item.MessageId,
            ChatId = item.ChatId,
            ProjectId = item.ProjectId,
            SenderId = item.SenderId,
            SenderName = item.SenderName,
            MessageType = item.MessageType?.ToString(),
            Content = item.Content!.Trim(),
            CreatedAt = item.CreatedAt
        };
    }
}
