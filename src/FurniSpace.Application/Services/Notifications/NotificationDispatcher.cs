using FurniSpace.Application.Common;
using FurniSpace.Application.Common.Notifications;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.Extensions.Logging;

namespace FurniSpace.Application.Services.Notifications;

public sealed class NotificationDispatcher : INotificationDispatcher
{
    private const string NotificationCreatedEvent = "notification.created";

    private readonly INotificationRepository _notifications;
    private readonly IRealtimeNotificationService _realtime;
    private readonly ILogger<NotificationDispatcher> _logger;
    private readonly IUnitOfWork _unitOfWork;

    public NotificationDispatcher(
        INotificationRepository notifications,
        IRealtimeNotificationService realtime,
        ILogger<NotificationDispatcher> logger,
        IUnitOfWork unitOfWork)
    {
        _notifications = notifications;
        _realtime = realtime;
        _logger = logger;
        _unitOfWork = unitOfWork;
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
            var envelope = new DispatchEnvelope(
                title,
                message,
                typeName,
                projectId,
                referenceType,
                referenceId,
                now);

            await DispatchInAppRealtimeAsync(
                receivers,
                envelope,
                cancellationToken);
        }
        else
        {
            await DispatchRealtimeOnlyAsync(receivers, template.SignalREventName, title, message, now);
        }
    }

    private async Task DispatchInAppRealtimeAsync(
        IReadOnlyList<Guid> receivers,
        DispatchEnvelope envelope,
        CancellationToken cancellationToken)
    {
        foreach (var receiverId in receivers)
        {
            var notification = new Notification
            {
                NotificationId = Guid.NewGuid(),
                ReceiverId = receiverId,
                ProjectId = envelope.ProjectId,
                Title = envelope.Title,
                Message = envelope.Message,
                NotificationType = envelope.TypeName,
                ReferenceType = envelope.ReferenceType,
                ReferenceId = envelope.ReferenceId,
                IsRead = false,
                CreatedAt = envelope.OccurredAt
            };

            try
            {
                await _notifications.AddAsync(notification, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Failed to persist notification of type {NotificationType} for receiver {ReceiverId}",
                    envelope.TypeName,
                    receiverId);
                continue;
            }

            var payload = new
            {
                notificationId = notification.NotificationId,
                title = envelope.Title,
                message = envelope.Message,
                notificationType = envelope.TypeName,
                projectId = envelope.ProjectId,
                referenceType = envelope.ReferenceType,
                referenceId = envelope.ReferenceId,
                createdAt = envelope.OccurredAt
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

    private sealed record DispatchEnvelope(
        string Title,
        string Message,
        string TypeName,
        Guid? ProjectId,
        string? ReferenceType,
        Guid? ReferenceId,
        DateTime OccurredAt);
}
