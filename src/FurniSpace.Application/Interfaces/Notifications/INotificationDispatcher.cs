using FurniSpace.Application.Common.Notifications;

namespace FurniSpace.Application.Interfaces.Notifications;

public interface INotificationDispatcher
{
    Task DispatchAsync(
        NotificationType type,
        IReadOnlyDictionary<string, string> parameters,
        IEnumerable<Guid> receiverIds,
        NotificationDispatchRequest? request = null,
        CancellationToken cancellationToken = default);
}
