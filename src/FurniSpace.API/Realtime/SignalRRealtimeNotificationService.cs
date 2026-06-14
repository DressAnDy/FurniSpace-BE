using FurniSpace.Application.Common.Realtime;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.API.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace FurniSpace.API.Realtime;

public sealed class SignalRRealtimeNotificationService : IRealtimeNotificationService
{
    private readonly IHubContext<NotificationsHub> _hub;

    public SignalRRealtimeNotificationService(IHubContext<NotificationsHub> hub)
    {
        _hub = hub;
    }

    public Task SendToUserAsync(
        Guid userId,
        string eventName,
        object payload,
        CancellationToken cancellationToken = default)
    {
        return _hub.Clients
            .Group(RealtimeGroupNames.User(userId))
            .SendAsync(eventName, payload, cancellationToken);
    }

    public Task SendToRoleAsync(
        string role,
        string eventName,
        object payload,
        CancellationToken cancellationToken = default)
    {
        return _hub.Clients
            .Group(RealtimeGroupNames.Role(role))
            .SendAsync(eventName, payload, cancellationToken);
    }

    public async Task SendToUsersAsync(
        IEnumerable<Guid> userIds,
        string eventName,
        object payload,
        CancellationToken cancellationToken = default)
    {
        foreach (var userId in userIds)
        {
            await _hub.Clients
                .Group(RealtimeGroupNames.User(userId))
                .SendAsync(eventName, payload, cancellationToken);
        }
    }
}
