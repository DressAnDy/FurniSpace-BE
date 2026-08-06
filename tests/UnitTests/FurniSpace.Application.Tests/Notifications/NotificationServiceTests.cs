#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.DTOs.Notifications;
using FurniSpace.Application.Services.Notifications;
using FurniSpace.Application.Tests.TestDoubles;
using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Xunit;

namespace FurniSpace.Application.Tests.Notifications;

public sealed class NotificationServiceTests
{
    [Fact]
    public async Task GetMyNotificationsAsync_ReturnsUnauthorized_WhenUserIdIsEmpty()
    {
        var service = new NotificationService(new FakeNotificationRepository(), TestUnitOfWork.Instance);

        var result = await service.GetMyNotificationsAsync(Guid.Empty, null, 1, 20);

        Assert.Equal(401, result.Status);
    }

    [Fact]
    public async Task GetMyNotificationsAsync_ReturnsBadRequest_WhenPageIsZero()
    {
        var service = new NotificationService(new FakeNotificationRepository(), TestUnitOfWork.Instance);

        var result = await service.GetMyNotificationsAsync(Guid.NewGuid(), null, page: 0, limit: 20);

        Assert.Equal(400, result.Status);
    }

    [Fact]
    public async Task GetMyNotificationsAsync_ReturnsBadRequest_WhenLimitExceedsMax()
    {
        var service = new NotificationService(new FakeNotificationRepository(), TestUnitOfWork.Instance);

        var result = await service.GetMyNotificationsAsync(Guid.NewGuid(), null, page: 1, limit: 101);

        Assert.Equal(400, result.Status);
    }

    [Fact]
    public async Task GetMyNotificationsAsync_ReturnsPagedResult()
    {
        var userId = Guid.NewGuid();
        var notification = CreateNotification(userId);
        var repo = new FakeNotificationRepository(pagedItems: [notification], pagedTotal: 1);
        var service = new NotificationService(repo, TestUnitOfWork.Instance);

        var result = await service.GetMyNotificationsAsync(userId, isUnread: true, page: 2, limit: 10);

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data.Total);
        Assert.Equal(2, result.Data.Page);
        Assert.Equal(10, result.Data.Limit);
        Assert.Single(result.Data.Items);
        Assert.Equal(userId, result.Data.Items[0].ReceiverId);
        Assert.True(repo.GetPagedIsUnread);
        Assert.Equal(2, repo.GetPagedPage);
        Assert.Equal(10, repo.GetPagedLimit);
    }

    [Fact]
    public async Task GetUnreadCountAsync_ReturnsUnauthorized_WhenUserIdIsEmpty()
    {
        var service = new NotificationService(new FakeNotificationRepository(), TestUnitOfWork.Instance);

        var result = await service.GetUnreadCountAsync(Guid.Empty);

        Assert.Equal(401, result.Status);
    }

    [Fact]
    public async Task GetUnreadCountAsync_ReturnsCount()
    {
        var userId = Guid.NewGuid();
        var repo = new FakeNotificationRepository(unreadCount: 5);
        var service = new NotificationService(repo, TestUnitOfWork.Instance);

        var result = await service.GetUnreadCountAsync(userId);

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(5, result.Data.UnreadCount);
    }

    [Fact]
    public async Task MarkAsReadAsync_ReturnsUnauthorized_WhenUserIdIsEmpty()
    {
        var service = new NotificationService(new FakeNotificationRepository(), TestUnitOfWork.Instance);

        var result = await service.MarkAsReadAsync(Guid.NewGuid(), Guid.Empty);

        Assert.Equal(401, result.Status);
    }

    [Fact]
    public async Task MarkAsReadAsync_ReturnsBadRequest_WhenNotificationIdIsEmpty()
    {
        var service = new NotificationService(new FakeNotificationRepository(), TestUnitOfWork.Instance);

        var result = await service.MarkAsReadAsync(Guid.Empty, Guid.NewGuid());

        Assert.Equal(400, result.Status);
    }

    [Fact]
    public async Task MarkAsReadAsync_ReturnsNotFound_WhenNotificationDoesNotExist()
    {
        var repo = new FakeNotificationRepository(activeNotification: null);
        var service = new NotificationService(repo, TestUnitOfWork.Instance);

        var result = await service.MarkAsReadAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(404, result.Status);
    }

    [Fact]
    public async Task MarkAsReadAsync_ReturnsSuccess_WhenAlreadyRead()
    {
        var userId = Guid.NewGuid();
        var notification = CreateNotification(userId, isRead: true);
        var repo = new FakeNotificationRepository(activeNotification: notification);
        var service = new NotificationService(repo, TestUnitOfWork.ForSaveChanges(repo.SaveChangesAsync));

        var result = await service.MarkAsReadAsync(notification.NotificationId, userId);

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.IsRead);
        Assert.Equal(0, repo.SaveChangesCallCount);
    }

    [Fact]
    public async Task MarkAsReadAsync_MarksNotificationAsRead_AndSaves()
    {
        var userId = Guid.NewGuid();
        var notification = CreateNotification(userId, isRead: false);
        var repo = new FakeNotificationRepository(activeNotification: notification);
        var service = new NotificationService(repo, TestUnitOfWork.ForSaveChanges(repo.SaveChangesAsync));

        var result = await service.MarkAsReadAsync(notification.NotificationId, userId);

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.IsRead);
        Assert.NotNull(result.Data.ReadAt);
        Assert.True(notification.IsRead);
        Assert.NotNull(notification.ReadAt);
        Assert.Equal(1, repo.SaveChangesCallCount);
    }

    [Fact]
    public async Task MarkAllAsReadAsync_ReturnsUnauthorized_WhenUserIdIsEmpty()
    {
        var service = new NotificationService(new FakeNotificationRepository(), TestUnitOfWork.Instance);

        var result = await service.MarkAllAsReadAsync(Guid.Empty);

        Assert.Equal(401, result.Status);
    }

    [Fact]
    public async Task MarkAllAsReadAsync_ReturnsSuccess_AndCallsMarkAll()
    {
        var userId = Guid.NewGuid();
        var repo = new FakeNotificationRepository();
        var service = new NotificationService(repo, TestUnitOfWork.Instance);

        var result = await service.MarkAllAsReadAsync(userId);

        Assert.Equal(200, result.Status);
        Assert.Equal(1, repo.MarkAllAsReadCallCount);
        Assert.Equal(userId, repo.MarkAllAsReadReceiverId);
    }

    private static Notification CreateNotification(Guid receiverId, bool isRead = false)
    {
        return new Notification
        {
            NotificationId = Guid.NewGuid(),
            ReceiverId = receiverId,
            Title = "Test notification",
            Message = "You have a new update.",
            NotificationType = "ProjectRequestSubmitted",
            IsRead = isRead,
            CreatedAt = DateTime.UtcNow
        };
    }

    private sealed class FakeNotificationRepository : INotificationRepository
    {
        private readonly IReadOnlyList<Notification> _pagedItems;
        private readonly int _pagedTotal;
        private readonly int _unreadCount;
        private readonly Notification? _activeNotification;

        public FakeNotificationRepository(
            IReadOnlyList<Notification>? pagedItems = null,
            int pagedTotal = 0,
            int unreadCount = 0,
            Notification? activeNotification = null)
        {
            _pagedItems = pagedItems ?? [];
            _pagedTotal = pagedTotal;
            _unreadCount = unreadCount;
            _activeNotification = activeNotification;
        }

        public int SaveChangesCallCount { get; private set; }
        public int MarkAllAsReadCallCount { get; private set; }
        public Guid MarkAllAsReadReceiverId { get; private set; }
        public bool? GetPagedIsUnread { get; private set; }
        public int GetPagedPage { get; private set; }
        public int GetPagedLimit { get; private set; }

        public Task<(IReadOnlyList<Notification> Items, int Total)> GetPagedByReceiverAsync(
            Guid receiverId, bool? isUnread, int page, int limit,
            CancellationToken cancellationToken = default)
        {
            GetPagedIsUnread = isUnread;
            GetPagedPage = page;
            GetPagedLimit = limit;
            return Task.FromResult((_pagedItems, _pagedTotal));
        }

        public Task<int> CountUnreadByReceiverAsync(
            Guid receiverId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_unreadCount);
        }

        public Task<Notification?> GetActiveByIdAndReceiverAsync(
            Guid notificationId, Guid receiverId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                _activeNotification?.NotificationId == notificationId ? _activeNotification : null);
        }

        public Task MarkAllAsReadAsync(
            Guid receiverId, DateTime readAt, CancellationToken cancellationToken = default)
        {
            MarkAllAsReadCallCount++;
            MarkAllAsReadReceiverId = receiverId;
            return Task.CompletedTask;
        }

        public IQueryable<Notification> Query() => Enumerable.Empty<Notification>().AsQueryable();
        public Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Notification>> ListAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task AddAsync(Notification entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddRangeAsync(IEnumerable<Notification> entities, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Update(Notification entity) { }
        public void Remove(Notification entity) { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCallCount++;
            return Task.FromResult(1);
        }
    }
}
