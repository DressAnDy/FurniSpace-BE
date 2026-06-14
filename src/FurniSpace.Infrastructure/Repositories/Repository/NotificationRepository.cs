using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.Repositories.Base;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Infrastructure.Repositories.Repository;

public sealed class NotificationRepository : GenericRepository<Notification>, INotificationRepository
{
    public NotificationRepository(AppDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<(IReadOnlyList<Notification> Items, int Total)> GetPagedByReceiverAsync(
        Guid receiverId,
        bool? isUnread,
        int page,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = DbContext.NotificationSet
            .Where(n => n.ReceiverId == receiverId && n.DeletedAt == null);

        if (isUnread.HasValue)
        {
            var unread = isUnread.Value;
            query = query.Where(n => n.IsRead != unread);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public Task<int> CountUnreadByReceiverAsync(
        Guid receiverId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.NotificationSet
            .CountAsync(
                n => n.ReceiverId == receiverId &&
                     !n.IsRead &&
                     n.DeletedAt == null,
                cancellationToken);
    }

    public Task<Notification?> GetActiveByIdAndReceiverAsync(
        Guid notificationId,
        Guid receiverId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.NotificationSet
            .FirstOrDefaultAsync(
                n => n.NotificationId == notificationId &&
                     n.ReceiverId == receiverId &&
                     n.DeletedAt == null,
                cancellationToken);
    }

    public Task MarkAllAsReadAsync(
        Guid receiverId,
        DateTime readAt,
        CancellationToken cancellationToken = default)
    {
        return DbContext.NotificationSet
            .Where(n => n.ReceiverId == receiverId &&
                        !n.IsRead &&
                        n.DeletedAt == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(n => n.IsRead, true)
                    .SetProperty(n => n.ReadAt, readAt),
                cancellationToken);
    }
}
