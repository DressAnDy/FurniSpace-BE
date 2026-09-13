namespace FurniSpace.Application.Interfaces.Notifications;

public interface IRealtimeNotificationService
{
    Task SendToUserAsync(Guid userId, string eventName, object payload, CancellationToken cancellationToken = default);

    Task SendToRoleAsync(string role, string eventName, object payload, CancellationToken cancellationToken = default);

    Task SendToUsersAsync(IEnumerable<Guid> userIds, string eventName, object payload, CancellationToken cancellationToken = default);
}
