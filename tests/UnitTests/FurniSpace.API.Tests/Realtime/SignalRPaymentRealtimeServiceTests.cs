#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Hubs;
using FurniSpace.API.Realtime;
using FurniSpace.Application.Common.Realtime;
using FurniSpace.Application.DTOs.Payments;
using FurniSpace.Domain.Enums;
using Microsoft.AspNetCore.SignalR;
using Xunit;

namespace FurniSpace.API.Tests.Realtime;

public sealed class SignalRPaymentRealtimeServiceTests
{
    [Fact]
    public async Task SendPaymentUpdatedAsync_SendsExpectedEventToPaymentGroup()
    {
        var paymentId = Guid.NewGuid();
        var payload = new PaymentUpdatedRealtimeDto
        {
            PaymentId = paymentId,
            ProjectId = Guid.NewGuid(),
            PaymentCode = "FS12345678",
            Status = PaymentStatus.PAID,
            AppliedAmount = 10000m
        };
        var clients = new FakeHubClients();
        var service = new SignalRPaymentRealtimeService(new FakeHubContext(clients));

        await service.SendPaymentUpdatedAsync(payload);

        Assert.Equal(PaymentRealtimeConstants.Payment(paymentId), clients.GroupNames[0]);
        Assert.Equal(PaymentRealtimeConstants.PaymentUpdatedEvent, clients.Proxy.Method);
        Assert.Same(payload, Assert.Single(clients.Proxy.Arguments!));
    }

    [Fact]
    public async Task SendPaymentUpdatedAsync_SendsExpectedEventToStakeholderGroups()
    {
        var paymentId = Guid.NewGuid();
        var stakeholderId = Guid.NewGuid();
        var payload = new PaymentUpdatedRealtimeDto
        {
            PaymentId = paymentId,
            ProjectId = Guid.NewGuid(),
            PaymentCode = "FS12345678",
            Status = PaymentStatus.PAID,
            AppliedAmount = 10000m
        };
        var clients = new FakeHubClients();
        var service = new SignalRPaymentRealtimeService(new FakeHubContext(clients));

        await service.SendPaymentUpdatedAsync(payload, [stakeholderId]);

        Assert.Equal(2, clients.GroupNames.Count);
        Assert.Equal(PaymentRealtimeConstants.Payment(paymentId), clients.GroupNames[0]);
        Assert.Equal(RealtimeGroupNames.User(stakeholderId), clients.GroupNames[1]);
    }

    private sealed class FakeHubContext : IHubContext<PaymentHub>
    {
        public FakeHubContext(IHubClients clients)
        {
            Clients = clients;
        }

        public IHubClients Clients { get; }
        public IGroupManager Groups { get; } = new NoOpGroupManager();
    }

    private sealed class FakeHubClients : IHubClients
    {
        public List<string> GroupNames { get; } = [];
        public FakeClientProxy Proxy { get; } = new();
        public IClientProxy All => Proxy;
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => Proxy;
        public IClientProxy Client(string connectionId) => Proxy;
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => Proxy;

        public IClientProxy Group(string groupName)
        {
            GroupNames.Add(groupName);
            return Proxy;
        }

        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => Proxy;
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => Proxy;
        public IClientProxy User(string userId) => Proxy;
        public IClientProxy Users(IReadOnlyList<string> userIds) => Proxy;
    }

    private sealed class FakeClientProxy : IClientProxy
    {
        public string? Method { get; private set; }
        public object?[]? Arguments { get; private set; }

        public Task SendCoreAsync(
            string method,
            object?[] args,
            CancellationToken cancellationToken = default)
        {
            Method = method;
            Arguments = args;
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpGroupManager : IGroupManager
    {
        public Task AddToGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RemoveFromGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
