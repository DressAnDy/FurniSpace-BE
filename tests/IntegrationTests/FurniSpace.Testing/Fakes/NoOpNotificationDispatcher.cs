using FurniSpace.Application.Common.Notifications;
using FurniSpace.Application.Interfaces.Notifications;

namespace FurniSpace.Testing.Fakes;

public sealed class NoOpNotificationDispatcher : INotificationDispatcher
{
    public Task DispatchAsync(
        NotificationType type,
        IReadOnlyDictionary<string, string> parameters,
        IEnumerable<Guid> receiverIds,
        NotificationDispatchRequest? request = null,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
