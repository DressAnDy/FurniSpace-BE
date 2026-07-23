#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Hubs;
using FurniSpace.Application.Common;
using FurniSpace.Application.Common.Realtime;
using FurniSpace.Application.DTOs.Orders;
using FurniSpace.Application.DTOs.Payments;
using FurniSpace.Application.Interfaces.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Xunit;

namespace FurniSpace.API.Tests.Hubs;

public sealed class PaymentHubTests
{
    [Fact]
    public void Hub_RequiresAuthenticationAndAllowedRoles()
    {
        var authorize = typeof(PaymentHub)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal("CUSTOMER,SALES,DESIGNER,ADMIN", authorize.Roles);
    }

    [Fact]
    public async Task JoinPayment_WithAccess_AddsPaymentGroup()
    {
        var paymentId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var groups = new FakeGroupManager();
        var hub = BuildHub(groups, accountId, canAccess: true);

        await hub.JoinPayment(paymentId);

        var added = Assert.Single(groups.AddedGroups);
        Assert.Equal(PaymentRealtimeConstants.Payment(paymentId), added.GroupName);
    }

    [Fact]
    public async Task JoinPayment_WithoutAccess_ThrowsHubException()
    {
        var hub = BuildHub(new FakeGroupManager(), Guid.NewGuid(), canAccess: false);

        var exception = await Assert.ThrowsAsync<HubException>(() => hub.JoinPayment(Guid.NewGuid()));

        Assert.Equal("You do not have access to this payment.", exception.Message);
    }

    [Fact]
    public async Task JoinPayment_WithoutAccountClaim_ThrowsHubException()
    {
        var hub = BuildHub(new FakeGroupManager());

        var exception = await Assert.ThrowsAsync<HubException>(() => hub.JoinPayment(Guid.NewGuid()));

        Assert.Equal("Authenticated account id is required.", exception.Message);
    }

    [Fact]
    public async Task LeavePayment_RemovesPaymentGroup()
    {
        var paymentId = Guid.NewGuid();
        var groups = new FakeGroupManager();
        var hub = BuildHub(groups, Guid.NewGuid(), canAccess: true);

        await hub.LeavePayment(paymentId);

        var removed = Assert.Single(groups.RemovedGroups);
        Assert.Equal(PaymentRealtimeConstants.Payment(paymentId), removed.GroupName);
    }

    private static PaymentHub BuildHub(
        FakeGroupManager groups,
        Guid? accountId = null,
        bool canAccess = true)
    {
        var claims = accountId.HasValue
            ? new[] { new Claim(ClaimTypes.NameIdentifier, accountId.Value.ToString()) }
            : Array.Empty<Claim>();
        return new PaymentHub(new FakePaymentService(canAccess))
        {
            Context = new FakeHubCallerContext(
                "connection-1",
                new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))),
            Groups = groups
        };
    }

    private sealed class FakePaymentService(bool canAccess) : IPaymentService
    {
        public Task<bool> CanAccessPaymentAsync(
            Guid paymentId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(canAccess);

        public Task<ServiceResult<PaymentDetailDto>> CreateDepositPaymentForOrderAsync(
            Guid orderId,
            Guid currentUserId,
            CreateOrderDepositPaymentRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<PaymentDetailDto>.Unauthorized());

        public Task<ServiceResult<PaymentDetailDto>> CreateRemainingPaymentForOrderAsync(
            Guid orderId,
            Guid currentUserId,
            CreateOrderRemainingPaymentRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<PaymentDetailDto>.Unauthorized());

        public Task<ServiceResult<PaymentDetailDto>> CreateProjectStartFeePaymentAsync(
            Guid projectId,
            Guid currentUserId,
            CreateProjectStartFeePaymentRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<PaymentDetailDto>.Unauthorized());

        public Task<ServiceResult<ProjectStartFeeStatusDto>> GetProjectStartFeeStatusAsync(
            Guid projectId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<ProjectStartFeeStatusDto>.Unauthorized());

        public Task<ServiceResult<PaymentDetailDto>> GetByIdAsync(
            Guid paymentId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<PaymentDetailDto>.Unauthorized());

        public Task<ServiceResult<PaymentListResponseDto>> GetListAsync(
            Guid currentUserId,
            PaymentQueryDto query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<PaymentListResponseDto>.Unauthorized());

        public Task<ServiceResult<PaymentTransactionListResponseDto>> GetTransactionsAsync(
            Guid paymentId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<PaymentTransactionListResponseDto>.Unauthorized());

        public Task<ServiceResult<PaymentStatusByCodeDto>> GetStatusByCodeAsync(
            string paymentCode,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<PaymentStatusByCodeDto>.Unauthorized());

        public Task<ServiceResult<SePayVietQrResponseDto>> GenerateSePayVietQrAsync(
            Guid paymentId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<SePayVietQrResponseDto>.Unauthorized());

        public Task<ServiceResult<PayOsPaymentLinkResponseDto>> CreatePayOsPaymentLinkAsync(
            Guid paymentId,
            Guid currentUserId,
            CreatePayOsPaymentLinkRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<PayOsPaymentLinkResponseDto>.Unauthorized());

        public Task<ServiceResult<PaymentTransactionAttemptResponseDto>> CreatePaymentTransactionAttemptAsync(
            Guid paymentId,
            Guid currentUserId,
            CreatePaymentTransactionAttemptRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<PaymentTransactionAttemptResponseDto>.Unauthorized());

        public Task<ServiceResult<PayOsConfirmWebhookResponseDto>> ConfirmPayOsWebhookAsync(
            PayOsConfirmWebhookRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<PayOsConfirmWebhookResponseDto>.Unauthorized());

        public Task<ServiceResult<PaymentDetailDto>> CreateTestPaymentAsync(
            Guid currentUserId,
            CreateTestPaymentRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<PaymentDetailDto>.Unauthorized());
    }

    private sealed class FakeGroupManager : IGroupManager
    {
        public List<(string ConnectionId, string GroupName)> AddedGroups { get; } = [];
        public List<(string ConnectionId, string GroupName)> RemovedGroups { get; } = [];

        public Task AddToGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default)
        {
            AddedGroups.Add((connectionId, groupName));
            return Task.CompletedTask;
        }

        public Task RemoveFromGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default)
        {
            RemovedGroups.Add((connectionId, groupName));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeHubCallerContext : HubCallerContext
    {
        public FakeHubCallerContext(string connectionId, ClaimsPrincipal user)
        {
            ConnectionId = connectionId;
            User = user;
        }

        public override string ConnectionId { get; }
        public override string? UserIdentifier => User?.FindFirstValue(ClaimTypes.NameIdentifier);
        public override ClaimsPrincipal? User { get; }
        public override IDictionary<object, object?> Items { get; } =
            new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override CancellationToken ConnectionAborted => CancellationToken.None;

        public override void Abort()
        {
        }
    }
}
