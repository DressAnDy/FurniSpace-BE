#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.DTOs.Payments;
using FurniSpace.Application.Interfaces.Payments;
using FurniSpace.Application.Services.Payments;
using FurniSpace.Application.Tests.TestDoubles;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.Payments;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Xunit;

namespace FurniSpace.Application.Tests.Payments;

public sealed class PayOsWebhookHandlerTests
{
    [Fact]
    public async Task ProcessAsync_WithValidWebhook_UpdatesTransactionAndPayment()
    {
        var paymentId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        const long orderCode = 202607080001L;
        var payment = CreatePayment(paymentId, projectId, 10000m);
        var transaction = CreatePendingTransaction(paymentId, projectId, orderCode, 10000m);
        var repository = new FakePayOsPaymentRepository
        {
            Payment = payment,
            Transaction = transaction
        };
        var realtime = new FakePaymentRealtimeService();
        var handler = CreateHandler(
            repository,
            new FakePayOsClient
            {
                VerifiedWebhook = new PayOsVerifiedWebhookData
                {
                    OrderCode = orderCode,
                    Amount = 10000,
                    Code = "00",
                    Reference = "FT24012345678",
                    PaymentLinkId = "plink-001"
                }
            },
            realtime);

        var result = await handler.ProcessAsync("{}");

        Assert.Equal(200, result.StatusCode);
        Assert.Equal(PaymentTransactionStatus.SUCCESS, repository.Transaction!.Status);
        Assert.Equal(PaymentStatus.PAID, repository.Payment!.Status);
        Assert.Equal(10000m, repository.Payment.PaidAmount);
        Assert.NotNull(realtime.LastPayload);
        Assert.Equal(PaymentStatus.PAID, realtime.LastPayload!.Status);
    }

    [Fact]
    public async Task ProcessAsync_WithDuplicateSuccessTransaction_ReturnsSuccessWithoutUpdatingPayment()
    {
        var paymentId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        const long orderCode = 202607080002L;
        var payment = CreatePayment(paymentId, projectId, 10000m);
        var transaction = CreatePendingTransaction(paymentId, projectId, orderCode, 10000m);
        transaction.Status = PaymentTransactionStatus.SUCCESS;
        var repository = new FakePayOsPaymentRepository
        {
            Payment = payment,
            Transaction = transaction
        };
        var handler = CreateHandler(
            repository,
            new FakePayOsClient
            {
                VerifiedWebhook = new PayOsVerifiedWebhookData
                {
                    OrderCode = orderCode,
                    Amount = 10000,
                    Code = "00"
                }
            },
            new FakePaymentRealtimeService());

        var result = await handler.ProcessAsync("{}");

        Assert.Equal(200, result.StatusCode);
        Assert.Equal(0m, repository.Payment!.PaidAmount);
    }

    [Fact]
    public async Task ProcessAsync_WithInvalidSignature_ReturnsUnauthorized()
    {
        var handler = CreateHandler(
            new FakePayOsPaymentRepository(),
            new FakePayOsClient { ThrowOnVerify = true },
            new FakePaymentRealtimeService());

        var result = await handler.ProcessAsync("{}");

        Assert.Equal(401, result.StatusCode);
    }

    private static PayOsWebhookHandler CreateHandler(
        FakePayOsPaymentRepository repository,
        FakePayOsClient payOsClient,
        FakePaymentRealtimeService realtime)
    {
        var unitOfWork = TestUnitOfWork.ForTransaction(
            _ => Task.CompletedTask,
            _ => Task.FromResult(1),
            _ => Task.CompletedTask,
            _ => Task.CompletedTask);

        return new PayOsWebhookHandler(
            repository,
            unitOfWork,
            payOsClient,
            realtime,
            new NoOpPaymentBusinessEffectService());
    }

    private sealed class NoOpPaymentBusinessEffectService : IPaymentBusinessEffectService
    {
        public Task ApplyAsync(Payment payment, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private static Payment CreatePayment(Guid paymentId, Guid projectId, decimal amount)
    {
        return new Payment
        {
            PaymentId = paymentId,
            ProjectId = projectId,
            PaymentCode = "FS12345678",
            Amount = amount,
            PaidAmount = 0m,
            RemainingAmount = amount,
            Currency = "VND",
            Status = PaymentStatus.PENDING
        };
    }

    private static PaymentTransaction CreatePendingTransaction(
        Guid paymentId,
        Guid projectId,
        long orderCode,
        decimal amount)
    {
        return new PaymentTransaction
        {
            PaymentTransactionId = Guid.NewGuid(),
            PaymentId = paymentId,
            ProjectId = projectId,
            TransactionCode = "TXN12345678",
            TransactionType = PaymentTransactionType.CHARGE,
            Amount = amount,
            Currency = "VND",
            PaymentProvider = PaymentProvider.PAYOS,
            PaymentMethod = PaymentMethod.PAYMENT_LINK,
            ProviderReferenceCode = orderCode.ToString(),
            Status = PaymentTransactionStatus.PENDING
        };
    }

    private sealed class FakePayOsPaymentRepository : IPaymentRepository
    {
        public Payment? Payment { get; set; }
        public PaymentTransaction? Transaction { get; set; }

        public Task<Payment?> GetByIdAsync(Guid paymentId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Payment?.PaymentId == paymentId ? Payment : null);
        }

        public Task<PaymentDetailReadModel?> GetDetailAsync(Guid paymentId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<PaymentDetailReadModel?>(null);
        }

        public Task<PaymentDetailReadModel?> GetDetailByPaymentCodeAsync(string paymentCode, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<PaymentDetailReadModel?>(null);
        }

        public Task<PaymentStatusByCodeReadModel?> GetStatusByPaymentCodeAsync(string paymentCode, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<PaymentStatusByCodeReadModel?>(null);
        }

        public Task<IReadOnlyList<PaymentListItemReadModel>> GetListAsync(PaymentQueryReadModel query, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<PaymentListItemReadModel>>([]);
        }

        public Task<IReadOnlyList<PaymentTransactionReadModel>> GetTransactionsByPaymentIdAsync(Guid paymentId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<PaymentTransactionReadModel>>([]);
        }

        public Task<bool> PaymentCodeExistsAsync(string paymentCode, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }

        public Task<bool> TransactionCodeExistsAsync(string transactionCode, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }

        public Task<bool> ProviderTransactionExistsAsync(PaymentProvider provider, string providerTransactionId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }

        public Task<bool> PayOsOrderCodeExistsAsync(string orderCode, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }

        public Task<PaymentTransaction?> GetTransactionByProviderReferenceAsync(
            PaymentProvider provider,
            string providerReferenceCode,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                Transaction is not null &&
                Transaction.PaymentProvider == provider &&
                Transaction.ProviderReferenceCode == providerReferenceCode
                    ? Transaction
                    : null);
        }

        public Task AddPaymentAsync(Payment payment, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task AddTransactionAsync(PaymentTransaction transaction, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public void UpdatePayment(Payment payment)
        {
            Payment = payment;
        }

        public void UpdateTransaction(PaymentTransaction transaction)
        {
            Transaction = transaction;
        }

        public Task<Payment?> GetByOrderAndTypeAsync(
            Guid orderId,
            PaymentType paymentType,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Payment?>(null);
        }

        public Task<Payment?> GetByProjectAndTypeAsync(
            Guid projectId,
            PaymentType paymentType,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Payment?>(null);
        }

        public Task<decimal> SumOrderScopedPaidAmountAsync(
            Guid orderId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(0m);
        }
    }

    private sealed class FakePayOsClient : IPayOsClient
    {
        public PayOsVerifiedWebhookData? VerifiedWebhook { get; set; }
        public bool ThrowOnVerify { get; set; }

        public Task<PayOsCreatePaymentLinkResult> CreatePaymentLinkAsync(
            PayOsCreatePaymentLinkRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PayOsCreatePaymentLinkResult
            {
                CheckoutUrl = "https://pay.payos.vn/web/test",
                PaymentLinkId = "plink-test"
            });
        }

        public Task<PayOsVerifiedWebhookData> VerifyWebhookAsync(string rawBody, CancellationToken cancellationToken = default)
        {
            if (ThrowOnVerify)
            {
                throw new InvalidOperationException("Invalid signature");
            }

            return Task.FromResult(VerifiedWebhook!);
        }

        public Task<string> ConfirmWebhookAsync(string webhookUrl, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(webhookUrl);
        }
    }

    private sealed class FakePaymentRealtimeService : IPaymentRealtimeService
    {
        public PaymentUpdatedRealtimeDto? LastPayload { get; private set; }

        public Task SendPaymentUpdatedAsync(PaymentUpdatedRealtimeDto payload, CancellationToken cancellationToken = default)
        {
            LastPayload = payload;
            return Task.CompletedTask;
        }
    }
}
