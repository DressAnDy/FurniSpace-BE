using FurniSpace.Application.Common.Notifications;
using FurniSpace.Application.Interfaces.Notifications;

namespace FurniSpace.Testing.Fakes;

public sealed class CapturingNotificationDispatcher : INotificationDispatcher
{
    private readonly List<CapturedNotification> _notifications = [];
    private readonly object _sync = new();

    public IReadOnlyList<CapturedNotification> Notifications
    {
        get
        {
            lock (_sync)
            {
                return _notifications.ToArray();
            }
        }
    }

    public Task DispatchAsync(
        NotificationType type,
        IReadOnlyDictionary<string, string> parameters,
        IEnumerable<Guid> receiverIds,
        NotificationDispatchRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var captured = new CapturedNotification(
            type,
            new Dictionary<string, string>(parameters),
            receiverIds.ToArray(),
            request?.ProjectId,
            request?.ReferenceType,
            request?.ReferenceId,
            request?.Metadata is null ? null : new Dictionary<string, object?>(request.Metadata));

        lock (_sync)
        {
            _notifications.Add(captured);
        }

        return Task.CompletedTask;
    }

    public void Clear()
    {
        lock (_sync)
        {
            _notifications.Clear();
        }
    }
}
