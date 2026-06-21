using FurniSpace.Application.DTOs.ProjectChatMessages;

namespace FurniSpace.Application.Interfaces.ProjectChatMessages;

public interface IProjectChatRealtimeService
{
    Task SendMessageSentAsync(
        Guid projectId,
        Guid chatId,
        ProjectChatMessageDto message,
        CancellationToken cancellationToken = default);
}
