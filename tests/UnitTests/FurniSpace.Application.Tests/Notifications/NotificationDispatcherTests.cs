#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.Common.Notifications;
using FurniSpace.Application.DTOs.Notifications;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Application.Services.Notifications;
using FurniSpace.Application.Tests.TestDoubles;
using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FurniSpace.Application.Tests.Notifications;

public sealed class NotificationDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_InApp_PersistsAndPushesStandardPayload()
    {
        var receiverId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var repository = new CapturingNotificationRepository();
        var realtime = new CapturingRealtimeNotificationService();
        var dispatcher = new NotificationDispatcher(
            repository,
            realtime,
            NullLogger<NotificationDispatcher>.Instance,
            TestUnitOfWork.Instance);

        await dispatcher.DispatchAsync(
            NotificationType.PaymentCreated,
            new Dictionary<string, string>
            {
                ["PaymentCode"] = "PAY-001",
                ["PaymentType"] = "DEPOSIT",
                ["Amount"] = "1000000",
                ["Currency"] = "VND"
            },
            [receiverId],
            projectId,
            "PAYMENT",
            paymentId,
            metadata: new Dictionary<string, object?> { ["paymentType"] = "DEPOSIT" });

        var saved = Assert.Single(repository.Added);
        Assert.Equal(receiverId, saved.ReceiverId);
        Assert.Equal("PAYMENT", saved.ReferenceType);
        Assert.Equal(paymentId, saved.ReferenceId);

        var sent = Assert.Single(realtime.Sent);
        Assert.Equal(receiverId, sent.UserId);
        Assert.Equal("payment.created", sent.EventName);
        var payload = Assert.IsType<RealtimeNotificationPayloadDto>(sent.Payload);
        Assert.Equal(saved.NotificationId, payload.NotificationId);
        Assert.Equal(projectId, payload.ProjectId);
        Assert.Equal("PAYMENT", payload.ReferenceType);
        Assert.Equal(paymentId, payload.ReferenceId);
        Assert.Equal("DEPOSIT", payload.Metadata!["paymentType"]);
    }

    [Fact]
    public async Task DispatchAsync_DuplicateInApp_SkipsPersistAndPush()
    {
        var receiverId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var repository = new CapturingNotificationRepository { DuplicateExists = true };
        var realtime = new CapturingRealtimeNotificationService();
        var dispatcher = new NotificationDispatcher(
            repository,
            realtime,
            NullLogger<NotificationDispatcher>.Instance,
            TestUnitOfWork.Instance);

        await dispatcher.DispatchAsync(
            NotificationType.PaymentPaid,
            new Dictionary<string, string>
            {
                ["PaymentCode"] = "PAY-001",
                ["PaymentType"] = "DEPOSIT",
                ["Amount"] = "1000000",
                ["Currency"] = "VND"
            },
            [receiverId],
            Guid.NewGuid(),
            "PAYMENT",
            paymentId);

        Assert.Empty(repository.Added);
        Assert.Empty(realtime.Sent);
        Assert.Equal(1, repository.DuplicateChecks);
    }

    [Fact]
    public async Task DispatchAsync_RealtimeOnly_DoesNotPersist()
    {
        var receiverId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var repository = new CapturingNotificationRepository();
        var realtime = new CapturingRealtimeNotificationService();
        var dispatcher = new NotificationDispatcher(
            repository,
            realtime,
            NullLogger<NotificationDispatcher>.Instance,
            TestUnitOfWork.Instance);

        await dispatcher.DispatchAsync(
            NotificationType.ProjectStatusChanged,
            new Dictionary<string, string>
            {
                ["ProjectName"] = "Cafe ABC",
                ["Status"] = "PROPOSAL_SELECTED"
            },
            [receiverId],
            projectId,
            "PROJECT",
            projectId);

        Assert.Empty(repository.Added);
        var sent = Assert.Single(realtime.Sent);
        Assert.Equal("project.status.changed", sent.EventName);
        var payload = Assert.IsType<RealtimeNotificationPayloadDto>(sent.Payload);
        Assert.Null(payload.NotificationId);
        Assert.Equal(projectId, payload.ProjectId);
        Assert.Equal("PROJECT", payload.ReferenceType);
    }

    private sealed class CapturingNotificationRepository : INotificationRepository
    {
        public List<Notification> Added { get; } = [];
        public bool DuplicateExists { get; init; }
        public int DuplicateChecks { get; private set; }

        public Task<bool> ExistsActiveDuplicateAsync(
            Guid receiverId,
            string notificationType,
            string? referenceType,
            Guid referenceId,
            CancellationToken cancellationToken = default)
        {
            DuplicateChecks++;
            return Task.FromResult(DuplicateExists);
        }

        public Task AddAsync(Notification entity, CancellationToken cancellationToken = default)
        {
            Added.Add(entity);
            return Task.CompletedTask;
        }

        public Task<(IReadOnlyList<Notification> Items, int Total)> GetPagedByReceiverAsync(
            Guid receiverId, bool? isUnread, int page, int limit,
            CancellationToken cancellationToken = default)
            => Task.FromResult<(IReadOnlyList<Notification>, int)>(([], 0));

        public Task<int> CountUnreadByReceiverAsync(
            Guid receiverId, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<Notification?> GetActiveByIdAndReceiverAsync(
            Guid notificationId, Guid receiverId, CancellationToken cancellationToken = default)
            => Task.FromResult<Notification?>(null);

        public Task MarkAllAsReadAsync(
            Guid receiverId, DateTime readAt, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public IQueryable<Notification> Query() => Enumerable.Empty<Notification>().AsQueryable();
        public Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<Notification?>(null);
        public Task<IReadOnlyList<Notification>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Notification>>([]);
        public Task AddRangeAsync(IEnumerable<Notification> entities, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public void Update(Notification entity) { }
        public void Remove(Notification entity) { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }

    private sealed class CapturingRealtimeNotificationService : IRealtimeNotificationService
    {
        public List<(Guid UserId, string EventName, object Payload)> Sent { get; } = [];

        public Task SendToUserAsync(
            Guid userId, string eventName, object payload, CancellationToken cancellationToken = default)
        {
            Sent.Add((userId, eventName, payload));
            return Task.CompletedTask;
        }

        public Task SendToRoleAsync(
            string role, string eventName, object payload, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SendToUsersAsync(
            IEnumerable<Guid> userIds, string eventName, object payload, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
