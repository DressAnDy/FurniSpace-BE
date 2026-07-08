#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.Common.Payments;
using FurniSpace.Application.DTOs.Payments;
using FurniSpace.Application.Interfaces.Payments;
using FurniSpace.Application.Tests.TestDoubles;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using Xunit;

namespace FurniSpace.Application.Tests.Payments;

public sealed class PaymentWebhookChargeSupportTests
{
    [Fact]
    public async Task CommitSuccessfulChargeAsync_PersistsPaymentAndTransaction()
    {
        var payment = CreatePayment();
        var transaction = CreateTransaction(payment.PaymentId);
        var repository = new PaymentServiceFakeRepository();
        repository.SeedPayment(payment);
        var unitOfWork = TestUnitOfWork.ForTransaction(
            _ => Task.CompletedTask,
            _ => Task.FromResult(1),
            _ => Task.CompletedTask,
            _ => Task.CompletedTask);

        await PaymentWebhookChargeSupport.CommitSuccessfulChargeAsync(
            unitOfWork,
            repository,
            new NoOpPaymentBusinessEffectService(),
            payment,
            transaction,
            isExistingTransaction: false,
            CancellationToken.None);

        var transactions = await repository.GetTransactionsByPaymentIdAsync(payment.PaymentId);
        Assert.Single(transactions);
    }

    [Fact]
    public async Task PushPaymentUpdatedAsync_WhenRealtimeFails_DoesNotThrow()
    {
        var payment = CreatePayment();
        var transaction = CreateTransaction(payment.PaymentId);
        var realtime = new FailingPaymentRealtimeService();

        var exception = await Record.ExceptionAsync(() =>
            PaymentWebhookChargeSupport.PushPaymentUpdatedAsync(
                realtime,
                logger: null,
                payment,
                transaction,
                appliedAmount: 10000m,
                DateTime.UtcNow,
                CancellationToken.None));

        Assert.Null(exception);
        Assert.True(realtime.SendAttempted);
    }

    private static Payment CreatePayment()
    {
        return new Payment
        {
            PaymentId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            PaymentCode = "FS12345678",
            Amount = 10000m,
            PaidAmount = 10000m,
            RemainingAmount = 0m,
            Currency = "VND",
            Status = PaymentStatus.PAID
        };
    }

    private static PaymentTransaction CreateTransaction(Guid paymentId)
    {
        return new PaymentTransaction
        {
            PaymentTransactionId = Guid.NewGuid(),
            PaymentId = paymentId,
            TransactionCode = "TXN-001",
            Amount = 10000m,
            Currency = "VND",
            Status = PaymentTransactionStatus.SUCCESS,
            CreatedAt = DateTime.UtcNow
        };
    }

    private sealed class NoOpPaymentBusinessEffectService : IPaymentBusinessEffectService
    {
        public Task ApplyAsync(Payment payment, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FailingPaymentRealtimeService : IPaymentRealtimeService
    {
        public bool SendAttempted { get; private set; }

        public Task SendPaymentUpdatedAsync(
            PaymentUpdatedRealtimeDto payload,
            CancellationToken cancellationToken = default)
        {
            SendAttempted = true;
            throw new InvalidOperationException("SignalR unavailable.");
        }
    }
}
