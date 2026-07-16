using FurniSpace.Application.Common;
using static FurniSpace.Application.Constants.Notifications.NotificationServiceConstants;
using FurniSpace.Application.DTOs.Notifications;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Infrastructure.Repositories.IRepository;
using FurniSpace.Infrastructure.Persistence;
using Mapster;

namespace FurniSpace.Application.Services.Notifications;

public sealed class NotificationService : INotificationService
{
    private readonly INotificationRepository _notifications;
    private readonly IUnitOfWork _unitOfWork;

    public NotificationService(INotificationRepository notifications, IUnitOfWork unitOfWork)
    {
        _notifications = notifications;
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<NotificationListResponseDto>> GetMyNotificationsAsync(
        Guid currentUserId,
        bool? isUnread,
        int page,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<NotificationListResponseDto>.Unauthorized(AuthenticatedAccountIdRequiredMessage);
        }

        var paginationError = ValidatePagination(page, limit);
        if (paginationError is not null)
        {
            return ServiceResult<NotificationListResponseDto>.BadRequest(paginationError);
        }

        var (items, total) = await _notifications.GetPagedByReceiverAsync(
            currentUserId, isUnread, page, limit, cancellationToken);

        return ServiceResult<NotificationListResponseDto>.Success(
            new NotificationListResponseDto
            {
                Items = items.Adapt<List<NotificationDto>>(),
                Page = page,
                Limit = limit,
                Total = total
            });
    }

    public async Task<ServiceResult<NotificationUnreadCountDto>> GetUnreadCountAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<NotificationUnreadCountDto>.Unauthorized(AuthenticatedAccountIdRequiredMessage);
        }

        var count = await _notifications.CountUnreadByReceiverAsync(currentUserId, cancellationToken);
        return ServiceResult<NotificationUnreadCountDto>.Success(
            new NotificationUnreadCountDto { UnreadCount = count });
    }

    public async Task<ServiceResult<MarkNotificationReadDto>> MarkAsReadAsync(
        Guid notificationId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<MarkNotificationReadDto>.Unauthorized(AuthenticatedAccountIdRequiredMessage);
        }

        if (notificationId == Guid.Empty)
        {
            return ServiceResult<MarkNotificationReadDto>.BadRequest("Notification id is required.");
        }

        var notification = await _notifications.GetActiveByIdAndReceiverAsync(
            notificationId, currentUserId, cancellationToken);

        if (notification is null)
        {
            return ServiceResult<MarkNotificationReadDto>.NotFound(NotificationNotFoundMessage);
        }

        if (notification.IsRead)
        {
            return ServiceResult<MarkNotificationReadDto>.Success(
                new MarkNotificationReadDto
                {
                    NotificationId = notification.NotificationId,
                    IsRead = true,
                    ReadAt = notification.ReadAt
                });
        }

        var now = DateTime.UtcNow;
        notification.IsRead = true;
        notification.ReadAt = now;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<MarkNotificationReadDto>.Success(
            new MarkNotificationReadDto
            {
                NotificationId = notification.NotificationId,
                IsRead = true,
                ReadAt = now
            },
            "Notification marked as read.");
    }

    public async Task<ServiceResult<object>> MarkAllAsReadAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<object>.Unauthorized(AuthenticatedAccountIdRequiredMessage);
        }

        var now = DateTime.UtcNow;
        await _notifications.MarkAllAsReadAsync(currentUserId, now, cancellationToken);
        return ServiceResult<object>.Success(new { }, "All notifications marked as read.");
    }

    private static string? ValidatePagination(int page, int limit)
    {
        if (page < 1)
        {
            return "Page must be greater than zero.";
        }

        if (limit is < 1 or > 100)
        {
            return "Limit must be between 1 and 100.";
        }

        return null;
    }
}
