using FurniSpace.API.Hubs;
using FurniSpace.Application.Common.Realtime;
using FurniSpace.Application.DTOs.ProjectChatMessages;
using FurniSpace.Application.Interfaces.ProjectChatMessages;
using Microsoft.AspNetCore.SignalR;

namespace FurniSpace.API.Realtime;

public sealed class SignalRProjectChatRealtimeService : IProjectChatRealtimeService
{
    private readonly IHubContext<ProjectChatHub> _hub;

    public SignalRProjectChatRealtimeService(IHubContext<ProjectChatHub> hub)
    {
        _hub = hub;
    }

    public Task SendMessageSentAsync(
        Guid projectId,
        Guid chatId,
        ProjectChatMessageDto message,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            projectId,
            chatId,
            message
        };

        return _hub.Clients
            .Group(ProjectChatRealtimeConstants.Chat(chatId))
            .SendAsync(
                ProjectChatRealtimeConstants.MessageSentEvent,
                payload,
                cancellationToken);
    }
}
