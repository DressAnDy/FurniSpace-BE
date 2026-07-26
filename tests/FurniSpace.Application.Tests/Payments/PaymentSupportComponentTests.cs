#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.Tests.TestDoubles;
using FurniSpace.Application.Common.Notifications;
using FurniSpace.Application.Common.Payments;
using FurniSpace.Application.DTOs.Payments;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.ReadModels.Payments;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FurniSpace.Application.Tests.Payments;

public sealed class PaymentSupportComponentTests
{
    private static readonly DateTime UtcNow = new(2026, 7, 26, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ActivePaymentResolver_IsActive_ReturnsFalseWhenExpired()
    {
        var payment = CreatePayment(PaymentStatus.PENDING, expiredAt: UtcNow.AddMinutes(-1));

        Assert.False(ActivePaymentResolver.IsActive(payment, UtcNow));
        Assert.True(ActivePaymentResolver.IsExpired(payment, UtcNow));
    }

    [Fact]
    public void ActivePaymentResolver_MarkExpired_SetsStatusAndUpdatedAt()
    {
        var payment = CreatePayment(PaymentStatus.PENDING, expiredAt: UtcNow.AddMinutes(-1));

        ActivePaymentResolver.MarkExpired(payment, UtcNow);

        Assert.Equal(PaymentStatus.EXPIRED, payment.Status);
        Assert.Equal(UtcNow, payment.UpdatedAt);
    }

    [Fact]
    public void PaymentPayableEvaluator_IsPayable_ReturnsFalseWhenAlreadyPaidViaTransaction()
    {
        var payment = CreatePayment(PaymentStatus.PENDING);

        Assert.False(PaymentPayableEvaluator.IsPayable(payment, hasSuccessfulTransaction: true, UtcNow));
    }

    [Fact]
    public void PaymentPayableEvaluator_IsPayable_WithStatusFields_ReturnsTrueForPending()
    {
        Assert.True(PaymentPayableEvaluator.IsPayable(
            PaymentStatus.PENDING,
            amount: 100m,
            expiredAt: UtcNow.AddHours(1),
            hasSuccessfulTransaction: false,
            UtcNow));
    }

    [Fact]
    public void PaymentExpirySynchronizer_TryMarkExpiredIfNeeded_MarksPendingPaymentExpired()
    {
        var payment = CreatePayment(PaymentStatus.PENDING, expiredAt: UtcNow.AddMinutes(-5));

        var marked = PaymentExpirySynchronizer.TryMarkExpiredIfNeeded(payment, UtcNow);

        Assert.True(marked);
        Assert.Equal(PaymentStatus.EXPIRED, payment.Status);
    }

    [Fact]
    public void PaymentExpirySynchronizer_TryMarkExpiredIfNeeded_ReturnsFalseForPaidPayment()
    {
        var payment = CreatePayment(PaymentStatus.PAID, expiredAt: UtcNow.AddMinutes(-5));

        Assert.False(PaymentExpirySynchronizer.TryMarkExpiredIfNeeded(payment, UtcNow));
    }

    [Fact]
    public void PaymentServiceManagementSupport_ValidatePagination_RejectsInvalidValues()
    {
        Assert.Equal("Page must be greater than zero.", PaymentServiceManagementSupport.ValidatePagination(0, 20));
        Assert.Equal("Page size must be between 1 and 100.", PaymentServiceManagementSupport.ValidatePagination(1, 0));
        Assert.Equal("Page size must be between 1 and 100.", PaymentServiceManagementSupport.ValidatePagination(1, 101));
        Assert.Null(PaymentServiceManagementSupport.ValidatePagination(1, 20));
    }

    [Fact]
    public void PaymentServiceManagementSupport_IsValidHttpsUrl_AcceptsOnlyHttps()
    {
        Assert.True(PaymentServiceManagementSupport.IsValidHttpsUrl("https://example.com/return"));
        Assert.False(PaymentServiceManagementSupport.IsValidHttpsUrl("http://example.com/return"));
        Assert.False(PaymentServiceManagementSupport.IsValidHttpsUrl("not-a-url"));
    }

    [Fact]
    public void PaymentServiceManagementSupport_ToListItemDto_SetsIsPayable()
    {
        var item = new PaymentListItemReadModel
        {
            PaymentId = Guid.NewGuid(),
            PaymentCode = "FS12345678",
            ProjectId = Guid.NewGuid(),
            Amount = 100m,
            Status = PaymentStatus.PENDING,
            ExpiredAt = UtcNow.AddHours(1)
        };

        PaymentListItemDto dto = PaymentServiceManagementSupport.ToListItemDto(item, hasSuccessfulTransaction: false, UtcNow);

        Assert.True(dto.IsPayable);
        Assert.Equal("FS12345678", dto.PaymentCode);
    }

    [Fact]
    public void PaymentServiceManagementSupport_ToAttemptResponse_MapsTransactionFields()
    {
        var paymentId = Guid.NewGuid();
        var transaction = new PaymentTransactionReadModel
        {
            PaymentTransactionId = Guid.NewGuid(),
            PaymentId = paymentId,
            TransactionCode = "TXN-001",
            Amount = 100m,
            Currency = "VND",
            Status = PaymentTransactionStatus.PENDING,
            PaymentProvider = PaymentProvider.SEPAY,
            PaymentMethod = PaymentMethod.QR_CODE,
            PaymentUrl = "https://vietqr.test"
        };
        var payment = CreatePayment(PaymentStatus.PROCESSING);

        var dto = PaymentServiceManagementSupport.ToAttemptResponse(transaction, payment);

        Assert.Equal(paymentId, dto.PaymentId);
        Assert.Equal(PaymentProvider.SEPAY, dto.PaymentProvider);
        Assert.Equal(PaymentStatus.PROCESSING, dto.PaymentStatus);
    }

    [Fact]
    public async Task PaymentServiceActivePaymentSupport_ResolveReusableActivePaymentAsync_MarksExpiredPayment()
    {
        var now = DateTime.UtcNow;
        var payment = CreatePayment(PaymentStatus.PENDING, expiredAt: now.AddMinutes(-10));
        var repository = new ActivePaymentSupportFakeRepository { Payment = payment };
        var saveChangesCalled = false;
        var unitOfWork = TestUnitOfWork.ForSaveChanges(_ =>
        {
            saveChangesCalled = true;
            return Task.FromResult(1);
        });

        var result = await PaymentServiceActivePaymentSupport.ResolveReusableActivePaymentAsync(
            repository,
            unitOfWork,
            payment,
            CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(PaymentStatus.EXPIRED, payment.Status);
        Assert.True(saveChangesCalled);
    }

    [Fact]
    public void PaymentServiceActivePaymentSupport_ToDetailDto_SetsReusedFlag()
    {
        var payment = CreatePayment(PaymentStatus.PENDING);
        var detail = new PaymentDetailReadModel
        {
            PaymentId = payment.PaymentId,
            PaymentCode = payment.PaymentCode,
            Amount = payment.Amount
        };

        var dto = PaymentServiceActivePaymentSupport.ToDetailDto(detail, payment, reused: true);

        Assert.True(dto.Reused);
        Assert.Equal(payment.PaymentCode, dto.PaymentCode);
    }

    [Fact]
    public async Task PaymentCustomerNotificationSupport_TryDispatchAsync_DispatchesToPaidBy()
    {
        var dispatcher = new PaymentNotificationFakeDispatcher();
        var payment = CreatePayment(PaymentStatus.PENDING);
        payment.PaidBy = Guid.NewGuid();

        await PaymentCustomerNotificationSupport.TryDispatchAsync(
            dispatcher,
            NullLogger.Instance,
            NotificationType.PaymentProcessing,
            payment);

        Assert.Single(dispatcher.Dispatched);
        Assert.Equal(NotificationType.PaymentProcessing, dispatcher.Dispatched[0].Type);
        Assert.Equal(payment.PaymentCode, dispatcher.Dispatched[0].Parameters[PaymentCustomerNotificationSupport.PaymentCodeParameter]);
    }

    [Fact]
    public async Task PaymentCustomerNotificationSupport_TryDispatchAsync_SkipsWhenPaidByMissing()
    {
        var dispatcher = new PaymentNotificationFakeDispatcher();
        var payment = CreatePayment(PaymentStatus.PENDING);
        payment.PaidBy = null;

        await PaymentCustomerNotificationSupport.TryDispatchAsync(
            dispatcher,
            NullLogger.Instance,
            NotificationType.PaymentProcessing,
            payment);

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task PaymentCustomerNotificationSupport_TryDispatchAsync_SwallowsDispatcherExceptions()
    {
        var payment = CreatePayment(PaymentStatus.PENDING);
        payment.PaidBy = Guid.NewGuid();

        await PaymentCustomerNotificationSupport.TryDispatchAsync(
            new ThrowingNotificationDispatcher(),
            NullLogger.Instance,
            NotificationType.PaymentExpired,
            payment);
    }

    private static Payment CreatePayment(PaymentStatus status, DateTime? expiredAt = null)
    {
        return new Payment
        {
            PaymentId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            PaymentCode = "FS12345678",
            Amount = 100m,
            Currency = "VND",
            Status = status,
            ExpiredAt = expiredAt
        };
    }

    private sealed class ActivePaymentSupportFakeRepository : IPaymentRepository
    {
        public Payment? Payment { get; set; }

        public void UpdatePayment(Payment payment) => Payment = payment;

        public Task<Payment?> GetByIdAsync(Guid paymentId, CancellationToken cancellationToken = default)
            => Task.FromResult(Payment?.PaymentId == paymentId ? Payment : null);

        public Task<PaymentDetailReadModel?> GetDetailAsync(Guid paymentId, CancellationToken cancellationToken = default)
            => Task.FromResult<PaymentDetailReadModel?>(null);

        public Task<PaymentDetailReadModel?> GetDetailByPaymentCodeAsync(string paymentCode, CancellationToken cancellationToken = default)
            => Task.FromResult<PaymentDetailReadModel?>(null);

        public Task<PaymentStatusByCodeReadModel?> GetStatusByPaymentCodeAsync(string paymentCode, CancellationToken cancellationToken = default)
            => Task.FromResult<PaymentStatusByCodeReadModel?>(null);

        public Task<IReadOnlyList<PaymentListItemReadModel>> GetListAsync(PaymentQueryReadModel query, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PaymentListItemReadModel>>([]);

        public Task<int> CountAsync(PaymentQueryReadModel query, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<PaymentSummaryReadModel> GetSummaryAsync(PaymentQueryReadModel query, DateTime utcNow, CancellationToken cancellationToken = default)
            => Task.FromResult(new PaymentSummaryReadModel());

        public Task<IReadOnlyList<Payment>> GetExpiredPaymentsForSyncAsync(PaymentQueryReadModel query, DateTime utcNow, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Payment>>([]);

        public Task<PaymentTransaction?> GetTransactionByIdAsync(Guid paymentTransactionId, CancellationToken cancellationToken = default)
            => Task.FromResult<PaymentTransaction?>(null);

        public Task<PaymentTransactionReadModel?> GetLatestPendingTransactionAsync(Guid paymentId, PaymentProvider provider, PaymentMethod method, CancellationToken cancellationToken = default)
            => Task.FromResult<PaymentTransactionReadModel?>(null);

        public Task<PaymentTransactionReadModel?> GetLatestTransactionAsync(Guid paymentId, CancellationToken cancellationToken = default)
            => Task.FromResult<PaymentTransactionReadModel?>(null);

        public Task<IReadOnlySet<Guid>> GetPaymentIdsWithSuccessfulTransactionAsync(IReadOnlyCollection<Guid> paymentIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>());

        public Task<IReadOnlyList<PaymentTransactionReadModel>> GetTransactionsByPaymentIdAsync(Guid paymentId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PaymentTransactionReadModel>>([]);

        public Task<bool> PaymentCodeExistsAsync(string paymentCode, CancellationToken cancellationToken = default) => Task.FromResult(false);

        public Task<bool> TransactionCodeExistsAsync(string transactionCode, CancellationToken cancellationToken = default) => Task.FromResult(false);

        public Task<bool> ProviderTransactionExistsAsync(PaymentProvider provider, string providerTransactionId, CancellationToken cancellationToken = default) => Task.FromResult(false);

        public Task<bool> PayOsOrderCodeExistsAsync(string orderCode, CancellationToken cancellationToken = default) => Task.FromResult(false);

        public Task<PaymentTransaction?> GetTransactionByProviderReferenceAsync(PaymentProvider provider, string providerReferenceCode, CancellationToken cancellationToken = default)
            => Task.FromResult<PaymentTransaction?>(null);

        public Task AddPaymentAsync(Payment payment, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task AddTransactionAsync(PaymentTransaction transaction, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void UpdateTransaction(PaymentTransaction transaction) { }

        public Task<Payment?> GetByOrderAndTypeAsync(Guid orderId, PaymentType paymentType, CancellationToken cancellationToken = default)
            => Task.FromResult<Payment?>(null);

        public Task<Payment?> GetByProjectAndTypeAsync(Guid projectId, PaymentType paymentType, CancellationToken cancellationToken = default)
            => Task.FromResult<Payment?>(null);

        public Task<decimal> SumOrderScopedPaidAmountAsync(Guid orderId, CancellationToken cancellationToken = default) => Task.FromResult(0m);

        public Task<bool> HasSuccessfulTransactionAsync(Guid paymentId, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class PaymentNotificationFakeDispatcher : INotificationDispatcher
    {
        public List<(NotificationType Type, IReadOnlyDictionary<string, string> Parameters)> Dispatched { get; } = [];

        public Task DispatchAsync(
            NotificationType type,
            IReadOnlyDictionary<string, string> parameters,
            IEnumerable<Guid> receiverIds,
            Guid? projectId = null,
            string? referenceType = null,
            Guid? referenceId = null,
            CancellationToken cancellationToken = default)
        {
            Dispatched.Add((type, parameters));
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingNotificationDispatcher : INotificationDispatcher
    {
        public Task DispatchAsync(
            NotificationType type,
            IReadOnlyDictionary<string, string> parameters,
            IEnumerable<Guid> receiverIds,
            Guid? projectId = null,
            string? referenceType = null,
            Guid? referenceId = null,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Dispatch failed.");
        }
    }
}
