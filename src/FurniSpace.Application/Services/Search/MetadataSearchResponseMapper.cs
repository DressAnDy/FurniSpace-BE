using FurniSpace.Application.DTOs.ProjectChatMessages;
using FurniSpace.Application.DTOs.ProjectFiles;
using FurniSpace.Infrastructure.Common.Search.Documents;

namespace FurniSpace.Application.Services.Search;

public static class ChatMessageSearchResponseMapper
{
    public static ProjectChatMessageSearchItemDto ToItem(ChatMessageSearchDocument document)
    {
        return new ProjectChatMessageSearchItemDto
        {
            MessageId = document.MessageId,
            ChatId = document.ChatId,
            ProjectId = document.ProjectId,
            SenderId = document.SenderId,
            SenderName = document.SenderName,
            MessageType = document.MessageType,
            Content = document.Content,
            CreatedAt = document.CreatedAt
        };
    }
}

public static class ProjectFileSearchResponseMapper
{
    public static ProjectFileSearchItemDto ToItem(ProjectFileSearchDocument document)
    {
        return new ProjectFileSearchItemDto
        {
            FileId = document.FileId,
            ProjectId = document.ProjectId,
            ReferenceType = document.ReferenceType,
            ReferenceId = document.ReferenceId,
            OriginalFileName = document.OriginalFileName,
            FileType = document.FileType,
            Visibility = document.Visibility,
            MimeType = document.MimeType,
            UploadedAt = document.UploadedAt
        };
    }
}
