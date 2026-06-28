using FurniSpace.Application.DTOs.ProjectChatMessages;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Application.Interfaces.ProjectChatMessages;

namespace FurniSpace.API.Cli;

internal sealed class NoOpRealtimeNotificationService : IRealtimeNotificationService
{
    public Task SendToUserAsync(
        Guid userId,
        string eventName,
        object payload,
        CancellationToken cancellationToken = default)
    {
        _ = userId;
        _ = eventName;
        _ = payload;
        return Task.CompletedTask;
    }

    public Task SendToRoleAsync(
        string role,
        string eventName,
        object payload,
        CancellationToken cancellationToken = default)
    {
        _ = role;
        _ = eventName;
        _ = payload;
        return Task.CompletedTask;
    }

    public Task SendToUsersAsync(
        IEnumerable<Guid> userIds,
        string eventName,
        object payload,
        CancellationToken cancellationToken = default)
    {
        _ = userIds;
        _ = eventName;
        _ = payload;
        return Task.CompletedTask;
    }
}

internal sealed class NoOpProjectChatRealtimeService : IProjectChatRealtimeService
{
    public Task SendMessageSentAsync(
        Guid projectId,
        Guid chatId,
        ProjectChatMessageDto message,
        CancellationToken cancellationToken = default)
    {
        _ = projectId;
        _ = chatId;
        _ = message;
        return Task.CompletedTask;
    }
}
