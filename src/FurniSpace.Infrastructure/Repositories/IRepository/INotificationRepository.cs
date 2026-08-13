using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.Repositories.Base;

namespace FurniSpace.Infrastructure.Repositories.IRepository;

public interface INotificationRepository : IGenericRepository<Notification>
{
    Task<(IReadOnlyList<Notification> Items, int Total)> GetPagedByReceiverAsync(
        Guid receiverId,
        bool? isUnread,
        int page,
        int limit,
        CancellationToken cancellationToken = default);

    Task<int> CountUnreadByReceiverAsync(
        Guid receiverId,
        CancellationToken cancellationToken = default);

    Task<Notification?> GetActiveByIdAndReceiverAsync(
        Guid notificationId,
        Guid receiverId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsActiveDuplicateAsync(
        Guid receiverId,
        string notificationType,
        string? referenceType,
        Guid referenceId,
        CancellationToken cancellationToken = default);

    Task MarkAllAsReadAsync(
        Guid receiverId,
        DateTime readAt,
        CancellationToken cancellationToken = default);
}
