#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.Common.Notifications;
using FurniSpace.Application.Common.Payments;
using FurniSpace.Application.Common.Projects;
using FurniSpace.Application.DTOs.Payments;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Application.Interfaces.Payments;
using FurniSpace.Application.Interfaces.Projects;
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
        var customerId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        const long orderCode = 202607080001L;
        var payment = CreatePayment(paymentId, projectId, 10000m);
        payment.PaidBy = customerId;
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
            realtime,
            stakeholders: new FakeStakeholderResolver(projectId, salesId));

        var result = await handler.ProcessAsync("{}");

        Assert.Equal(200, result.StatusCode);
        Assert.Equal(PaymentTransactionStatus.SUCCESS, repository.Transaction!.Status);
        Assert.Equal(PaymentStatus.PAID, repository.Payment!.Status);
        Assert.Equal(10000m, repository.Payment.Amount);
        Assert.NotNull(realtime.LastPayload);
        Assert.Equal(PaymentStatus.PAID, realtime.LastPayload!.Status);
        Assert.Contains(customerId, realtime.LastStakeholderUserIds!);
        Assert.Contains(salesId, realtime.LastStakeholderUserIds!);
    }

    [Fact]
    public async Task ProcessAsync_WithFailedWebhook_NotifiesPayer()
    {
        var paymentId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var payerId = Guid.NewGuid();
        const long orderCode = 202607080011L;
        var payment = CreatePayment(paymentId, projectId, 10000m);
        payment.PaidBy = payerId;
        var transaction = CreatePendingTransaction(paymentId, projectId, orderCode, 10000m);
        var repository = new FakePayOsPaymentRepository
        {
            Payment = payment,
            Transaction = transaction
        };
        var dispatcher = new CapturingNotificationDispatcher();
        var handler = CreateHandler(
            repository,
            new FakePayOsClient
            {
                VerifiedWebhook = new PayOsVerifiedWebhookData
                {
                    OrderCode = orderCode,
                    Amount = 10000,
                    Code = "01"
                }
            },
            new FakePaymentRealtimeService(),
            notifications: dispatcher);

        var result = await handler.ProcessAsync("{}");

        Assert.Equal(200, result.StatusCode);
        Assert.Equal(PaymentTransactionStatus.FAILED, repository.Transaction!.Status);
        Assert.Equal(NotificationType.PaymentTransactionFailed, Assert.Single(dispatcher.Types));
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
        Assert.Equal(PaymentStatus.PENDING, repository.Payment!.Status);
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

    [Fact]
    public async Task ProcessAsync_WithMissingOrderCode_ReturnsBadRequest()
    {
        var handler = CreateHandler(
            new FakePayOsPaymentRepository(),
            new FakePayOsClient
            {
                VerifiedWebhook = new PayOsVerifiedWebhookData { OrderCode = 0, Code = "00" }
            },
            new FakePaymentRealtimeService());

        var result = await handler.ProcessAsync("{}");

        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task ProcessAsync_WithNonSuccessCode_ReturnsSuccessWithoutUpdating()
    {
        var handler = CreateHandler(
            new FakePayOsPaymentRepository(),
            new FakePayOsClient
            {
                VerifiedWebhook = new PayOsVerifiedWebhookData { OrderCode = 123, Code = "01" }
            },
            new FakePaymentRealtimeService());

        var result = await handler.ProcessAsync("{}");

        Assert.Equal(200, result.StatusCode);
    }

    [Fact]
    public async Task ProcessAsync_WithAmountMismatch_ReturnsBadRequest()
    {
        var paymentId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        const long orderCode = 202607080003L;
        var payment = CreatePayment(paymentId, projectId, 10000m);
        var transaction = CreatePendingTransaction(paymentId, projectId, orderCode, 10000m);
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
                    Amount = 5000,
                    Code = "00"
                }
            },
            new FakePaymentRealtimeService());

        var result = await handler.ProcessAsync("{}");

        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task ProcessAsync_WithCancelledPayment_ReturnsSuccessWithoutUpdating()
    {
        var paymentId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        const long orderCode = 202607080004L;
        var payment = CreatePayment(paymentId, projectId, 10000m);
        payment.Status = PaymentStatus.CANCELLED;
        var transaction = CreatePendingTransaction(paymentId, projectId, orderCode, 10000m);
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
        Assert.Equal(PaymentStatus.CANCELLED, repository.Payment!.Status);
    }

    [Fact]
    public async Task ProcessAsync_WithDuplicateProviderTransaction_ReturnsSuccessWithoutUpdating()
    {
        var paymentId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        const long orderCode = 202607080005L;
        var payment = CreatePayment(paymentId, projectId, 10000m);
        var transaction = CreatePendingTransaction(paymentId, projectId, orderCode, 10000m);
        var repository = new FakePayOsPaymentRepository
        {
            Payment = payment,
            Transaction = transaction,
            ProviderTransactionExists = true
        };
        var handler = CreateHandler(
            repository,
            new FakePayOsClient
            {
                VerifiedWebhook = new PayOsVerifiedWebhookData
                {
                    OrderCode = orderCode,
                    Amount = 10000,
                    Code = "00",
                    Reference = "FT24012345678"
                }
            },
            new FakePaymentRealtimeService());

        var result = await handler.ProcessAsync("{}");

        Assert.Equal(200, result.StatusCode);
        Assert.Equal(PaymentStatus.PENDING, repository.Payment!.Status);
    }

    [Fact]
    public async Task ProcessAsync_WithMissingTransaction_ReturnsSuccessWithoutUpdating()
    {
        var handler = CreateHandler(
            new FakePayOsPaymentRepository(),
            new FakePayOsClient
            {
                VerifiedWebhook = new PayOsVerifiedWebhookData
                {
                    OrderCode = 202607080006L,
                    Amount = 10000,
                    Code = "00"
                }
            },
            new FakePaymentRealtimeService());

        var result = await handler.ProcessAsync("{}");

        Assert.Equal(200, result.StatusCode);
    }

    private static PayOsWebhookHandler CreateHandler(
        FakePayOsPaymentRepository repository,
        FakePayOsClient payOsClient,
        FakePaymentRealtimeService realtime,
        IProjectStakeholderResolver? stakeholders = null,
        INotificationDispatcher? notifications = null)
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
            new PaymentWebhookRuntime(
                realtime,
                new NoOpPaymentBusinessEffectService(),
                notifications,
                stakeholders));
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
        public bool ProviderTransactionExists { get; set; }

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
            return Task.FromResult(ProviderTransactionExists);
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

        public Task<int> CountAsync(PaymentQueryReadModel query, CancellationToken cancellationToken = default)
            => PaymentRepositoryStubMethods.CountAsync(query, cancellationToken);

        public Task<PaymentSummaryReadModel> GetSummaryAsync(
            PaymentQueryReadModel query,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
            => PaymentRepositoryStubMethods.GetSummaryAsync(query, utcNow, cancellationToken);

        public Task<IReadOnlyList<Payment>> GetExpiredPaymentsForSyncAsync(
            PaymentQueryReadModel query,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
            => PaymentRepositoryStubMethods.GetExpiredPaymentsForSyncAsync(query, utcNow, cancellationToken);

        public Task<PaymentTransaction?> GetTransactionByIdAsync(
            Guid paymentTransactionId,
            CancellationToken cancellationToken = default)
            => PaymentRepositoryStubMethods.GetTransactionByIdAsync(paymentTransactionId, cancellationToken);

        public Task<PaymentTransactionReadModel?> GetLatestPendingTransactionAsync(
            Guid paymentId,
            PaymentProvider provider,
            PaymentMethod method,
            CancellationToken cancellationToken = default)
            => PaymentRepositoryStubMethods.GetLatestPendingTransactionAsync(
                paymentId,
                provider,
                method,
                cancellationToken);

        public Task<PaymentTransactionReadModel?> GetLatestTransactionAsync(
            Guid paymentId,
            CancellationToken cancellationToken = default)
            => PaymentRepositoryStubMethods.GetLatestTransactionAsync(paymentId, cancellationToken);

        public Task<IReadOnlySet<Guid>> GetPaymentIdsWithSuccessfulTransactionAsync(
            IReadOnlyCollection<Guid> paymentIds,
            CancellationToken cancellationToken = default)
            => PaymentRepositoryStubMethods.GetPaymentIdsWithSuccessfulTransactionAsync(paymentIds, cancellationToken);

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

        public Task<bool> HasSuccessfulTransactionAsync(
            Guid paymentId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                Transaction is not null &&
                Transaction.PaymentId == paymentId &&
                Transaction.Status == PaymentTransactionStatus.SUCCESS);
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
        public IReadOnlyCollection<Guid>? LastStakeholderUserIds { get; private set; }

        public Task SendPaymentUpdatedAsync(
            PaymentUpdatedRealtimeDto payload,
            IReadOnlyCollection<Guid>? stakeholderUserIds = null,
            CancellationToken cancellationToken = default)
        {
            LastPayload = payload;
            LastStakeholderUserIds = stakeholderUserIds;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeStakeholderResolver(Guid projectId, Guid salesId) : IProjectStakeholderResolver
    {
        public Task<ProjectStakeholders?> ResolveAsync(
            Guid requestedProjectId,
            CancellationToken cancellationToken = default)
        {
            if (requestedProjectId != projectId)
            {
                return Task.FromResult<ProjectStakeholders?>(null);
            }

            return Task.FromResult<ProjectStakeholders?>(new ProjectStakeholders
            {
                CustomerId = Guid.NewGuid(),
                AssignedSalesId = salesId
            });
        }
    }

    private sealed class CapturingNotificationDispatcher : INotificationDispatcher
    {
        public List<NotificationType> Types { get; } = [];

        public Task DispatchAsync(
            NotificationType type,
            IReadOnlyDictionary<string, string> parameters,
            IEnumerable<Guid> receiverIds,
            NotificationDispatchRequest? request = null,
            CancellationToken cancellationToken = default)
        {
            Types.Add(type);
            return Task.CompletedTask;
        }
    }
}
