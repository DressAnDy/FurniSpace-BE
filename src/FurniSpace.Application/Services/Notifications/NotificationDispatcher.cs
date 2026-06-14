using FurniSpace.Application.Common.Notifications;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.Extensions.Logging;

namespace FurniSpace.Application.Services.Notifications;

public sealed class NotificationDispatcher : INotificationDispatcher
{
    private const string NotificationCreatedEvent = "notification.created";

    private readonly INotificationRepository _notifications;
    private readonly IRealtimeNotificationService _realtime;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        INotificationRepository notifications,
        IRealtimeNotificationService realtime,
        ILogger<NotificationDispatcher> logger)
    {
        _notifications = notifications;
        _realtime = realtime;
        _logger = logger;
    }

    public async Task DispatchAsync(
        NotificationType type,
        IReadOnlyDictionary<string, string> parameters,
        IEnumerable<Guid> receiverIds,
        Guid? projectId = null,
        string? referenceType = null,
        Guid? referenceId = null,
        CancellationToken cancellationToken = default)
    {
        var template = NotificationTemplateProvider.Get(type);
        var title = NotificationTemplateProvider.RenderTitle(template, parameters);
        var message = NotificationTemplateProvider.RenderMessage(template, parameters);
        var typeName = type.ToString();
        var now = DateTime.UtcNow;
        var receivers = receiverIds.ToList();

        if (template.DeliveryLevel == NotificationDeliveryLevel.InAppRealtime)
        {
            await DispatchInAppRealtimeAsync(
                receivers, title, message, typeName, projectId,
                referenceType, referenceId, now, cancellationToken);
        }
        else
        {
            await DispatchRealtimeOnlyAsync(receivers, template.SignalREventName, title, message, now);
        }
    }

    private async Task DispatchInAppRealtimeAsync(
        IReadOnlyList<Guid> receivers,
        string title,
        string message,
        string typeName,
        Guid? projectId,
        string? referenceType,
        Guid? referenceId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        foreach (var receiverId in receivers)
        {
            var notification = new Notification
            {
                NotificationId = Guid.NewGuid(),
                ReceiverId = receiverId,
                ProjectId = projectId,
                Title = title,
                Message = message,
                NotificationType = typeName,
                ReferenceType = referenceType,
                ReferenceId = referenceId,
                IsRead = false,
                CreatedAt = now
            };

            try
            {
                await _notifications.AddAsync(notification, cancellationToken);
                await _notifications.SaveChangesAsync(cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Failed to persist notification of type {NotificationType} for receiver {ReceiverId}",
                    typeName,
                    receiverId);
                continue;
            }

            var payload = new
            {
                notificationId = notification.NotificationId,
                title,
                message,
                notificationType = typeName,
                projectId,
                referenceType,
                referenceId,
                createdAt = now
            };

            try
            {
                await _realtime.SendToUserAsync(receiverId, NotificationCreatedEvent, payload, cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Failed to push realtime event {EventName} to user {UserId}",
                    NotificationCreatedEvent,
                    receiverId);
            }
        }
    }

    private async Task DispatchRealtimeOnlyAsync(
        IReadOnlyList<Guid> receivers,
        string eventName,
        string title,
        string message,
        DateTime now)
    {
        var payload = new { title, message, occurredAt = now };

        foreach (var receiverId in receivers)
        {
            try
            {
                await _realtime.SendToUserAsync(receiverId, eventName, payload);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Failed to push realtime-only event {EventName} to user {UserId}",
                    eventName,
                    receiverId);
            }
        }
    }
}
