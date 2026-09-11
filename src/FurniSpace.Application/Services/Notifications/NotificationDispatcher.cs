using FurniSpace.Application.Common.Notifications;
using FurniSpace.Application.DTOs.Notifications;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.Extensions.Logging;

namespace FurniSpace.Application.Services.Notifications;

public sealed class NotificationDispatcher : INotificationDispatcher
{
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
        NotificationDispatchRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var template = NotificationTemplateProvider.Get(type);
        var title = NotificationTemplateProvider.RenderTitle(template, parameters);
        var message = NotificationTemplateProvider.RenderMessage(template, parameters);
        var typeName = type.ToString();
        var now = DateTime.UtcNow;
        var receivers = receiverIds
            .Where(receiverId => receiverId != Guid.Empty)
            .Distinct()
            .ToList();

        if (receivers.Count == 0)
        {
            return;
        }

        var envelope = new DispatchEnvelope(
            title,
            message,
            typeName,
            request?.ProjectId,
            request?.ReferenceType,
            request?.ReferenceId,
            now,
            template.SignalREventName,
            request?.Metadata);

        if (template.DeliveryLevel == NotificationDeliveryLevel.InAppRealtime)
        {
            await DispatchInAppRealtimeAsync(receivers, envelope, cancellationToken);
        }
        else
        {
            await DispatchRealtimeOnlyAsync(receivers, envelope, cancellationToken);
        }
    }

    private async Task DispatchInAppRealtimeAsync(
        IReadOnlyList<Guid> receivers,
        DispatchEnvelope envelope,
        CancellationToken cancellationToken)
    {
        foreach (var receiverId in receivers)
        {
            if (envelope.ReferenceId.HasValue)
            {
                var isDuplicate = await _notifications.ExistsActiveDuplicateAsync(
                    receiverId,
                    envelope.TypeName,
                    envelope.ReferenceType,
                    envelope.ReferenceId.Value,
                    cancellationToken);
                if (isDuplicate)
                {
                    _logger.LogInformation(
                        "Skipped duplicate in-app notification {NotificationType} for receiver {ReceiverId} reference {ReferenceId}",
                        envelope.TypeName,
                        receiverId,
                        envelope.ReferenceId);
                    continue;
                }
            }

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

            var payload = BuildPayload(
                notification.NotificationId,
                envelope);

            try
            {
                await _realtime.SendToUserAsync(receiverId, envelope.SignalREventName, payload, cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Failed to push realtime event {EventName} to user {UserId}",
                    envelope.SignalREventName,
                    receiverId);
            }
        }
    }

    private async Task DispatchRealtimeOnlyAsync(
        IReadOnlyList<Guid> receivers,
        DispatchEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var payload = BuildPayload(notificationId: null, envelope);

        foreach (var receiverId in receivers)
        {
            try
            {
                await _realtime.SendToUserAsync(receiverId, envelope.SignalREventName, payload, cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Failed to push realtime-only event {EventName} to user {UserId}",
                    envelope.SignalREventName,
                    receiverId);
            }
        }
    }

    private static RealtimeNotificationPayloadDto BuildPayload(
        Guid? notificationId,
        DispatchEnvelope envelope)
    {
        return new RealtimeNotificationPayloadDto
        {
            NotificationId = notificationId,
            Title = envelope.Title,
            Message = envelope.Message,
            NotificationType = envelope.TypeName,
            ProjectId = envelope.ProjectId,
            ReferenceType = envelope.ReferenceType,
            ReferenceId = envelope.ReferenceId,
            CreatedAt = envelope.OccurredAt,
            OccurredAt = envelope.OccurredAt,
            Metadata = envelope.Metadata
        };
    }

    private sealed record DispatchEnvelope(
        string Title,
        string Message,
        string TypeName,
        Guid? ProjectId,
        string? ReferenceType,
        Guid? ReferenceId,
        DateTime OccurredAt,
        string SignalREventName,
        IReadOnlyDictionary<string, object?>? Metadata);
}
