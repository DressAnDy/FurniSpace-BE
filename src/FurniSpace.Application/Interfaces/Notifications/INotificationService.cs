using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Notifications;

namespace FurniSpace.Application.Interfaces.Notifications;

public interface INotificationService
{
    Task<ServiceResult<NotificationListResponseDto>> GetMyNotificationsAsync(
        Guid currentUserId,
        bool? isUnread,
        int page,
        int limit,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<NotificationUnreadCountDto>> GetUnreadCountAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<MarkNotificationReadDto>> MarkAsReadAsync(
        Guid notificationId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<object>> MarkAllAsReadAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default);
}
