#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FurniSpace.Application.Common.Payments;
using FurniSpace.Application.DTOs.Payments;
using FurniSpace.Application.Interfaces.Payments;
using FurniSpace.Application.Services.Payments;
using FurniSpace.Application.Tests.TestDoubles;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.Payments;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.Extensions.Options;
using Xunit;

namespace FurniSpace.Application.Tests.Payments;

public sealed class SePayWebhookHandlerTests
{
    private const string WebhookSecret = "whsec_test_secret";
    private const string PaymentCode = "FS12345678";

    [Fact]
    public async Task ProcessAsync_WithValidWebhook_CreatesTransactionAndUpdatesPayment()
    {
        var paymentId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var payment = CreatePayment(paymentId, projectId, PaymentCode, 10000m);
        var repository = new FakePaymentRepository
        {
            Payment = payment,
            PaymentDetail = CreatePaymentDetail(paymentId, projectId, PaymentCode, 10000m)
        };
        var realtime = new FakePaymentRealtimeService();
        var handler = CreateHandler(repository, realtime);
        var rawBody = CreateWebhookPayload(id: 92704, transferAmount: 10000m, paymentCode: PaymentCode);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signature = ComputeSignature(WebhookSecret, timestamp, rawBody);

        var result = await handler.ProcessAsync(rawBody, signature, timestamp);

        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Body);
        Assert.True(result.Body!.Success);
        Assert.Single(repository.AddedTransactions);
        Assert.Equal(PaymentStatus.PAID, repository.Payment!.Status);
        Assert.Equal(10000m, repository.Payment.PaidAmount);
        Assert.Equal(0m, repository.Payment.RemainingAmount);
        Assert.Equal(1, repository.SaveChangesCallCount);
        Assert.NotNull(realtime.LastPayload);
        Assert.Equal(paymentId, realtime.LastPayload!.PaymentId);
        Assert.Equal(PaymentStatus.PAID, realtime.LastPayload.Status);
        Assert.Equal(10000m, realtime.LastPayload.AppliedAmount);
    }

    [Fact]
    public async Task ProcessAsync_WithPartialPayment_PushesPartiallyPaidRealtimeEvent()
    {
        var paymentId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var payment = CreatePayment(paymentId, projectId, PaymentCode, 30000m);
        var repository = new FakePaymentRepository
        {
            Payment = payment,
            PaymentDetail = CreatePaymentDetail(paymentId, projectId, PaymentCode, 30000m)
        };
        var realtime = new FakePaymentRealtimeService();
        var handler = CreateHandler(repository, realtime);
        var rawBody = CreateWebhookPayload(id: 92705, transferAmount: 10000m, paymentCode: PaymentCode);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signature = ComputeSignature(WebhookSecret, timestamp, rawBody);

        var result = await handler.ProcessAsync(rawBody, signature, timestamp);

        Assert.Equal(200, result.StatusCode);
        Assert.Equal(PaymentStatus.PARTIALLY_PAID, repository.Payment!.Status);
        Assert.Equal(10000m, repository.Payment.PaidAmount);
        Assert.Equal(20000m, repository.Payment.RemainingAmount);
        Assert.NotNull(realtime.LastPayload);
        Assert.Equal(PaymentStatus.PARTIALLY_PAID, realtime.LastPayload!.Status);
        Assert.Equal(20000m, realtime.LastPayload.RemainingAmount);
    }

    [Fact]
    public async Task ProcessAsync_WithDuplicateProviderTransaction_ReturnsSuccessWithoutUpdatingPayment()
    {
        var paymentId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var repository = new FakePaymentRepository
        {
            Payment = CreatePayment(paymentId, projectId, PaymentCode, 10000m),
            PaymentDetail = CreatePaymentDetail(paymentId, projectId, PaymentCode, 10000m),
            ProviderTransactionExists = true
        };
        var handler = CreateHandler(repository, new FakePaymentRealtimeService());
        var rawBody = CreateWebhookPayload(id: 92704, transferAmount: 10000m, paymentCode: PaymentCode);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signature = ComputeSignature(WebhookSecret, timestamp, rawBody);

        var result = await handler.ProcessAsync(rawBody, signature, timestamp);

        Assert.Equal(200, result.StatusCode);
        Assert.Empty(repository.AddedTransactions);
        Assert.Equal(0m, repository.Payment!.PaidAmount);
    }

    [Fact]
    public async Task ProcessAsync_WithInvalidSignature_ReturnsUnauthorized()
    {
        var handler = CreateHandler(new FakePaymentRepository(), new FakePaymentRealtimeService());
        var rawBody = CreateWebhookPayload(id: 92704, transferAmount: 10000m, paymentCode: PaymentCode);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        var result = await handler.ProcessAsync(rawBody, "sha256=invalid", timestamp);

        Assert.Equal(401, result.StatusCode);
        Assert.Equal("Webhook signature is invalid.", result.ErrorMessage);
    }

    private static SePayWebhookHandler CreateHandler(
        FakePaymentRepository repository,
        FakePaymentRealtimeService paymentRealtime)
    {
        var options = Options.Create(new SePayOptions
        {
            WebhookSecret = WebhookSecret,
            BankAccountNo = "1017588888",
            PaymentCodeRegex = @"FS[0-9]{8,10}",
            StrictAmountCheck = true,
            AllowOverpayment = false
        });
        var unitOfWork = TestUnitOfWork.ForTransaction(
            _ => Task.CompletedTask,
            _ => Task.FromResult(++repository.SaveChangesCallCount),
            _ => Task.CompletedTask,
            _ => Task.CompletedTask);

        return new SePayWebhookHandler(
            repository,
            unitOfWork,
            options,
            new SePayWebhookSignatureVerifier(options),
            paymentRealtime);
    }

    private static Payment CreatePayment(Guid paymentId, Guid projectId, string paymentCode, decimal amount)
    {
        return new Payment
        {
            PaymentId = paymentId,
            ProjectId = projectId,
            PaymentCode = paymentCode,
            Amount = amount,
            PaidAmount = 0m,
            RemainingAmount = amount,
            Currency = "VND",
            Status = PaymentStatus.PENDING
        };
    }

    private static PaymentDetailReadModel CreatePaymentDetail(
        Guid paymentId,
        Guid projectId,
        string paymentCode,
        decimal amount)
    {
        return new PaymentDetailReadModel
        {
            PaymentId = paymentId,
            ProjectId = projectId,
            PaymentCode = paymentCode,
            Amount = amount,
            PaidAmount = 0m,
            RemainingAmount = amount,
            Currency = "VND",
            Status = PaymentStatus.PENDING,
            CustomerId = Guid.NewGuid()
        };
    }

    private static string CreateWebhookPayload(long id, decimal transferAmount, string paymentCode)
    {
        var payload = new SePayWebhookPayloadDto
        {
            Id = id,
            AccountNumber = "1017588888",
            Code = paymentCode,
            Content = $"{paymentCode} chuyen tien",
            TransferType = "in",
            TransferAmount = transferAmount,
            ReferenceCode = "FT24012345678",
            TransactionDate = "2024-07-02 11:08:33"
        };

        return JsonSerializer.Serialize(payload);
    }

    private static string ComputeSignature(string secret, string timestamp, string rawBody)
    {
        var message = $"{timestamp}.{rawBody}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private sealed class FakePaymentRepository : IPaymentRepository
    {
        public Payment? Payment { get; set; }
        public PaymentDetailReadModel? PaymentDetail { get; set; }
        public bool ProviderTransactionExists { get; set; }
        public List<PaymentTransaction> AddedTransactions { get; } = [];
        public int SaveChangesCallCount { get; set; }

        public Task<Payment?> GetByIdAsync(Guid paymentId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Payment?.PaymentId == paymentId ? Payment : null);
        }

        public Task<PaymentDetailReadModel?> GetDetailAsync(Guid paymentId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(PaymentDetail?.PaymentId == paymentId ? PaymentDetail : null);
        }

        public Task<PaymentDetailReadModel?> GetDetailByPaymentCodeAsync(string paymentCode, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                PaymentDetail is not null && PaymentDetail.PaymentCode == paymentCode
                    ? PaymentDetail
                    : null);
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
            return Task.FromResult<PaymentTransaction?>(null);
        }

        public Task AddPaymentAsync(Payment payment, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task AddTransactionAsync(PaymentTransaction transaction, CancellationToken cancellationToken = default)
        {
            AddedTransactions.Add(transaction);
            return Task.CompletedTask;
        }

        public void UpdatePayment(Payment payment)
        {
            Payment = payment;
        }

        public void UpdateTransaction(PaymentTransaction transaction)
        {
        }
    }

    private sealed class FakePaymentRealtimeService : IPaymentRealtimeService
    {
        public PaymentUpdatedRealtimeDto? LastPayload { get; private set; }

        public Task SendPaymentUpdatedAsync(
            PaymentUpdatedRealtimeDto payload,
            CancellationToken cancellationToken = default)
        {
            LastPayload = payload;
            return Task.CompletedTask;
        }
    }
}
