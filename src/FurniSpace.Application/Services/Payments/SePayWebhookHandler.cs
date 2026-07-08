using System.Globalization;
using System.Text.Json;
using FurniSpace.Application.Common.Payments;
using FurniSpace.Application.DTOs.Payments;
using FurniSpace.Application.Interfaces.Payments;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.ReadModels.Payments;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FurniSpace.Application.Services.Payments;

public sealed class SePayWebhookHandler : ISePayWebhookService
{
    private const int MaxTransactionCodeAttempts = 5;
    private static readonly JsonSerializerOptions PayloadSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IPaymentRepository _payments;
    private readonly IUnitOfWork _unitOfWork;
    private readonly SePayOptions _options;
    private readonly SePayWebhookSignatureVerifier _signatureVerifier;
    private readonly IPaymentRealtimeService _paymentRealtime;
    private readonly ILogger<SePayWebhookHandler>? _logger;

    public SePayWebhookHandler(
        IPaymentRepository payments,
        IUnitOfWork unitOfWork,
        IOptions<SePayOptions> options,
        SePayWebhookSignatureVerifier signatureVerifier,
        IPaymentRealtimeService paymentRealtime,
        ILogger<SePayWebhookHandler>? logger = null)
    {
        _payments = payments;
        _unitOfWork = unitOfWork;
        _options = options.Value;
        _signatureVerifier = signatureVerifier;
        _paymentRealtime = paymentRealtime;
        _logger = logger;
    }

    public async Task<SePayWebhookProcessResult> ProcessAsync(
        string rawBody,
        string? signature,
        string? timestampHeader,
        CancellationToken cancellationToken = default)
    {
        var verification = _signatureVerifier.Verify(rawBody, signature, timestampHeader);
        if (!verification.IsValid)
        {
            _logger?.LogWarning("SePay webhook signature invalid: {ErrorMessage}", verification.ErrorMessage);
            return new SePayWebhookProcessResult(401, null, verification.ErrorMessage);
        }

        SePayWebhookPayloadDto? payload;
        try
        {
            payload = JsonSerializer.Deserialize<SePayWebhookPayloadDto>(rawBody, PayloadSerializerOptions);
        }
        catch (JsonException exception)
        {
            _logger?.LogWarning(exception, "Failed to deserialize SePay webhook payload.");
            return new SePayWebhookProcessResult(400, null, "Invalid webhook payload.");
        }

        if (payload is null)
        {
            return new SePayWebhookProcessResult(400, null, "Invalid webhook payload.");
        }

        var providerTransactionId = payload.Id.ToString(CultureInfo.InvariantCulture);
        if (!string.Equals(payload.TransferType, "in", StringComparison.OrdinalIgnoreCase))
        {
            _logger?.LogInformation(
                "SePay webhook ignored: transfer type is not incoming. ProviderTransactionId={ProviderTransactionId}",
                providerTransactionId);
            return SuccessResult();
        }

        if (!string.Equals(payload.AccountNumber, _options.BankAccountNo, StringComparison.OrdinalIgnoreCase))
        {
            _logger?.LogWarning(
                "SePay webhook rejected: account number mismatch. ProviderTransactionId={ProviderTransactionId}, AccountNumber={AccountNumber}",
                providerTransactionId,
                payload.AccountNumber);
            return SuccessResult();
        }

        var paymentCode = SePayPaymentCodeExtractor.Extract(
            payload.Code,
            payload.Content,
            rawBody,
            _options.PaymentCodeRegex);

        if (string.IsNullOrWhiteSpace(paymentCode))
        {
            _logger?.LogInformation(
                "SePay webhook ignored: payment code not found. ProviderTransactionId={ProviderTransactionId}",
                providerTransactionId);
            return SuccessResult();
        }

        if (await _payments.ProviderTransactionExistsAsync(PaymentProvider.SEPAY, providerTransactionId, cancellationToken))
        {
            _logger?.LogInformation(
                "SePay webhook duplicate ignored. ProviderTransactionId={ProviderTransactionId}, PaymentCode={PaymentCode}",
                providerTransactionId,
                paymentCode);
            return SuccessResult();
        }

        var paymentDetail = await _payments.GetDetailByPaymentCodeAsync(paymentCode, cancellationToken);
        if (paymentDetail is null)
        {
            _logger?.LogWarning(
                "SePay webhook failed: payment not found. ProviderTransactionId={ProviderTransactionId}, PaymentCode={PaymentCode}",
                providerTransactionId,
                paymentCode);
            return SuccessResult();
        }

        var payment = await _payments.GetByIdAsync(paymentDetail.PaymentId, cancellationToken);
        if (payment is null)
        {
            return SuccessResult();
        }

        if (payment.Status is PaymentStatus.CANCELLED or PaymentStatus.EXPIRED or PaymentStatus.REFUNDED)
        {
            _logger?.LogInformation(
                "SePay webhook ignored: payment is not collectable. PaymentId={PaymentId}, Status={Status}",
                payment.PaymentId,
                payment.Status);
            return SuccessResult();
        }

        if (payload.TransferAmount <= 0m)
        {
            _logger?.LogWarning(
                "SePay webhook failed: transfer amount must be greater than zero. PaymentId={PaymentId}, ProviderTransactionId={ProviderTransactionId}",
                payment.PaymentId,
                providerTransactionId);
            return SuccessResult();
        }

        if (_options.StrictAmountCheck &&
            payload.TransferAmount > payment.RemainingAmount &&
            !_options.AllowOverpayment)
        {
            _logger?.LogWarning(
                "SePay overpayment detected for payment {PaymentId}. TransferAmount={TransferAmount}, RemainingAmount={RemainingAmount}",
                payment.PaymentId,
                payload.TransferAmount,
                payment.RemainingAmount);
            return SuccessResult();
        }

        var transactionCode = await GenerateUniqueTransactionCodeAsync(cancellationToken);
        var transactionTime = ParseTransactionTime(payload.TransactionDate);
        var now = DateTime.UtcNow;
        var transaction = new PaymentTransaction
        {
            PaymentTransactionId = Guid.NewGuid(),
            PaymentId = payment.PaymentId,
            ProjectId = payment.ProjectId,
            OrderId = payment.OrderId,
            TransactionCode = transactionCode,
            TransactionType = PaymentTransactionType.CHARGE,
            Amount = payload.TransferAmount,
            Currency = payment.Currency,
            PaymentProvider = PaymentProvider.SEPAY,
            PaymentMethod = PaymentMethod.QR_CODE,
            ProviderTransactionId = providerTransactionId,
            ProviderReferenceCode = payload.ReferenceCode,
            Status = PaymentTransactionStatus.SUCCESS,
            TransactionTime = transactionTime,
            RawProviderPayload = rawBody,
            CreatedAt = now
        };

        var appliedAmount = Math.Min(payload.TransferAmount, payment.RemainingAmount);
        payment.PaidAmount += appliedAmount;
        payment.RemainingAmount = Math.Max(0m, payment.Amount - payment.PaidAmount);
        payment.Status = payment.RemainingAmount <= 0m
            ? PaymentStatus.PAID
            : PaymentStatus.PARTIALLY_PAID;
        payment.PaidAt = payment.Status == PaymentStatus.PAID ? now : payment.PaidAt;
        payment.UpdatedAt = now;

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _payments.AddTransactionAsync(transaction, cancellationToken);
            _payments.UpdatePayment(payment);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        _logger?.LogInformation(
            "SePay webhook processed. PaymentId={PaymentId}, ProviderTransactionId={ProviderTransactionId}, AppliedAmount={AppliedAmount}, Status={Status}",
            payment.PaymentId,
            providerTransactionId,
            appliedAmount,
            payment.Status);

        await PushPaymentUpdatedAsync(
            payment,
            transaction,
            appliedAmount,
            now,
            cancellationToken);

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

    private async Task<string> GenerateUniqueTransactionCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxTransactionCodeAttempts; attempt++)
        {
            var code = TransactionCodeGenerator.Generate();
            if (!await _payments.TransactionCodeExistsAsync(code, cancellationToken))
            {
                return code;
            }
        }

        throw new InvalidOperationException("Unable to generate a unique transaction code.");
    }

    private static DateTime? ParseTransactionTime(string? transactionDate)
    {
        if (string.IsNullOrWhiteSpace(transactionDate))
        {
            return null;
        }

        return DateTime.TryParse(
            transactionDate,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal,
            out var parsed)
            ? parsed.ToUniversalTime()
            : null;
    }

    private static SePayWebhookProcessResult SuccessResult()
    {
        return new SePayWebhookProcessResult(200, new SePayWebhookSuccessDto(), null);
    }
}
