using System.Globalization;
using FurniSpace.Application.Common.Payments;
using FurniSpace.Application.DTOs.Payments;
using FurniSpace.Application.Interfaces.Payments;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.Extensions.Logging;

namespace FurniSpace.Application.Services.Payments;

public sealed class PayOsWebhookHandler : IPayOsWebhookService
{
    private readonly IPaymentRepository _payments;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPayOsClient _payOsClient;
    private readonly IPaymentRealtimeService _paymentRealtime;
    private readonly IPaymentBusinessEffectService _paymentBusinessEffects;
    private readonly ILogger<PayOsWebhookHandler>? _logger;

    public PayOsWebhookHandler(
        IPaymentRepository payments,
        IUnitOfWork unitOfWork,
        IPayOsClient payOsClient,
        IPaymentRealtimeService paymentRealtime,
        IPaymentBusinessEffectService paymentBusinessEffects,
        ILogger<PayOsWebhookHandler>? logger = null)
    {
        _payments = payments;
        _unitOfWork = unitOfWork;
        _payOsClient = payOsClient;
        _paymentRealtime = paymentRealtime;
        _paymentBusinessEffects = paymentBusinessEffects;
        _logger = logger;
    }

    public async Task<PayOsWebhookProcessResult> ProcessAsync(
        string rawBody,
        CancellationToken cancellationToken = default)
    {
        PayOsVerifiedWebhookData webhookData;
        try
        {
            webhookData = await _payOsClient.VerifyWebhookAsync(rawBody, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger?.LogWarning(exception, "PayOS webhook signature verification failed.");
            return new PayOsWebhookProcessResult(401, null, "PayOS webhook signature is invalid.");
        }

        if (webhookData.OrderCode <= 0)
        {
            return new PayOsWebhookProcessResult(400, null, "PayOS order code is missing.");
        }

        if (!IsSuccessfulWebhook(webhookData.Code))
        {
            _logger?.LogInformation(
                "PayOS webhook ignored: payment not successful. OrderCode={OrderCode}, Code={Code}",
                webhookData.OrderCode,
                webhookData.Code);
            return SuccessResult();
        }

        var orderCode = webhookData.OrderCode.ToString(CultureInfo.InvariantCulture);
        var transaction = await _payments.GetTransactionByProviderReferenceAsync(
            PaymentProvider.PAYOS,
            orderCode,
            cancellationToken);

        if (transaction is null)
        {
            _logger?.LogWarning(
                "PayOS webhook failed: transaction not found. OrderCode={OrderCode}",
                webhookData.OrderCode);
            return SuccessResult();
        }

        if (transaction.Status == PaymentTransactionStatus.SUCCESS)
        {
            _logger?.LogInformation(
                "PayOS webhook duplicate ignored. OrderCode={OrderCode}, PaymentTransactionId={PaymentTransactionId}",
                webhookData.OrderCode,
                transaction.PaymentTransactionId);
            return SuccessResult();
        }

        var payment = await _payments.GetByIdAsync(transaction.PaymentId, cancellationToken);
        if (payment is null)
        {
            return SuccessResult();
        }

        if (payment.Status is PaymentStatus.CANCELLED or PaymentStatus.EXPIRED or PaymentStatus.REFUNDED or PaymentStatus.PAID)
        {
            _logger?.LogInformation(
                "PayOS webhook ignored: payment is not collectable. PaymentId={PaymentId}, Status={Status}",
                payment.PaymentId,
                payment.Status);
            return SuccessResult();
        }

        var transactionAmount = decimal.Truncate(transaction.Amount);
        var webhookAmount = webhookData.Amount;
        if (webhookAmount != (long)transactionAmount)
        {
            _logger?.LogWarning(
                "PayOS amount mismatch. PaymentTransactionId={PaymentTransactionId}, TransactionAmount={TransactionAmount}, WebhookAmount={WebhookAmount}",
                transaction.PaymentTransactionId,
                transactionAmount,
                webhookAmount);
            return new PayOsWebhookProcessResult(400, null, "PayOS webhook amount does not match transaction amount.");
        }

        var providerTransactionId = ResolveProviderTransactionId(webhookData);
        if (!string.IsNullOrWhiteSpace(providerTransactionId) &&
            await _payments.ProviderTransactionExistsAsync(PaymentProvider.PAYOS, providerTransactionId, cancellationToken))
        {
            _logger?.LogInformation(
                "PayOS webhook duplicate provider transaction ignored. ProviderTransactionId={ProviderTransactionId}",
                providerTransactionId);
            return SuccessResult();
        }

        var now = DateTime.UtcNow;
        var appliedAmount = Math.Min(transactionAmount, payment.RemainingAmount);

        transaction.Status = PaymentTransactionStatus.SUCCESS;
        transaction.ProviderTransactionId = providerTransactionId;
        transaction.ProviderReferenceCode = orderCode;
        transaction.TransactionTime = ParseTransactionTime(webhookData.TransactionDateTime) ?? now;
        transaction.RawProviderPayload = rawBody;

        PaymentSummaryCalculator.ApplyCharge(payment, appliedAmount, now);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _payments.UpdateTransaction(transaction);
            _payments.UpdatePayment(payment);
            await _paymentBusinessEffects.ApplyAsync(payment, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        _logger?.LogInformation(
            "PayOS webhook processed. PaymentId={PaymentId}, OrderCode={OrderCode}, AppliedAmount={AppliedAmount}, Status={Status}",
            payment.PaymentId,
            webhookData.OrderCode,
            appliedAmount,
            payment.Status);

        await PushPaymentUpdatedAsync(payment, transaction, appliedAmount, now, cancellationToken);
        return SuccessResult();
    }

    private async Task PushPaymentUpdatedAsync(
        Payment payment,
        PaymentTransaction transaction,
        decimal appliedAmount,
        DateTime occurredAt,
        CancellationToken cancellationToken)
    {
        var payload = new PaymentUpdatedRealtimeDto
        {
            PaymentId = payment.PaymentId,
            ProjectId = payment.ProjectId,
            PaymentCode = payment.PaymentCode,
            Status = payment.Status,
            Amount = payment.Amount,
            PaidAmount = payment.PaidAmount,
            RemainingAmount = payment.RemainingAmount,
            PaymentTransactionId = transaction.PaymentTransactionId,
            TransactionAmount = transaction.Amount,
            AppliedAmount = appliedAmount,
            PaidAt = payment.PaidAt,
            OccurredAt = occurredAt
        };

        try
        {
            await _paymentRealtime.SendPaymentUpdatedAsync(payload, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger?.LogWarning(
                exception,
                "Failed to push payment.updated realtime event. PaymentId={PaymentId}, PaymentTransactionId={PaymentTransactionId}",
                payment.PaymentId,
                transaction.PaymentTransactionId);
        }
    }

    private static bool IsSuccessfulWebhook(string? code)
    {
        return string.Equals(code, "00", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveProviderTransactionId(PayOsVerifiedWebhookData webhookData)
    {
        if (!string.IsNullOrWhiteSpace(webhookData.Reference))
        {
            return webhookData.Reference;
        }

        return string.IsNullOrWhiteSpace(webhookData.PaymentLinkId)
            ? null
            : webhookData.PaymentLinkId;
    }

    private static DateTime? ParseTransactionTime(string? transactionDateTime)
    {
        if (string.IsNullOrWhiteSpace(transactionDateTime))
        {
            return null;
        }

        return DateTime.TryParse(
            transactionDateTime,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal,
            out var parsed)
            ? parsed.ToUniversalTime()
            : null;
    }

    private static PayOsWebhookProcessResult SuccessResult()
    {
        return new PayOsWebhookProcessResult(200, new PayOsWebhookSuccessDto(), null);
    }
}
