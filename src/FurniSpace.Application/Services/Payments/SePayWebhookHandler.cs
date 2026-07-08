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
    private readonly IPaymentBusinessEffectService _paymentBusinessEffects;
    private readonly ILogger<SePayWebhookHandler>? _logger;

    public SePayWebhookHandler(
        IPaymentRepository payments,
        IUnitOfWork unitOfWork,
        IOptions<SePayOptions> options,
        SePayWebhookSignatureVerifier signatureVerifier,
        IPaymentRealtimeService paymentRealtime,
        IPaymentBusinessEffectService paymentBusinessEffects,
        ILogger<SePayWebhookHandler>? logger = null)
    {
        _payments = payments;
        _unitOfWork = unitOfWork;
        _options = options.Value;
        _signatureVerifier = signatureVerifier;
        _paymentRealtime = paymentRealtime;
        _paymentBusinessEffects = paymentBusinessEffects;
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

        var payload = DeserializePayload(rawBody);
        if (payload is null)
        {
            return new SePayWebhookProcessResult(400, null, "Invalid webhook payload.");
        }

        var providerTransactionId = payload.Id.ToString(CultureInfo.InvariantCulture);
        if (!IsIncomingTransferForConfiguredAccount(payload, providerTransactionId))
        {
            return SuccessResult();
        }

        var paymentCode = ExtractPaymentCode(payload, rawBody);
        if (string.IsNullOrWhiteSpace(paymentCode))
        {
            return SuccessResult();
        }

        if (await IsDuplicateProviderTransactionAsync(providerTransactionId, paymentCode, cancellationToken))
        {
            return SuccessResult();
        }

        var payment = await LoadCollectablePaymentAsync(paymentCode, providerTransactionId, cancellationToken);
        if (payment is null || !CanApplyTransferAmount(payment, payload, providerTransactionId))
        {
            return SuccessResult();
        }

        await PersistSuccessfulTransferAsync(
            payment,
            payload,
            rawBody,
            providerTransactionId,
            cancellationToken);

        return SuccessResult();
    }

    private SePayWebhookPayloadDto? DeserializePayload(string rawBody)
    {
        try
        {
            return JsonSerializer.Deserialize<SePayWebhookPayloadDto>(rawBody, PayloadSerializerOptions);
        }
        catch (JsonException exception)
        {
            _logger?.LogWarning(exception, "Failed to deserialize SePay webhook payload.");
            return null;
        }
    }

    private bool IsIncomingTransferForConfiguredAccount(SePayWebhookPayloadDto payload, string providerTransactionId)
    {
        if (!string.Equals(payload.TransferType, "in", StringComparison.OrdinalIgnoreCase))
        {
            _logger?.LogInformation(
                "SePay webhook ignored: transfer type is not incoming. ProviderTransactionId={ProviderTransactionId}",
                providerTransactionId);
            return false;
        }

        if (string.Equals(payload.AccountNumber, _options.BankAccountNo, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        _logger?.LogWarning(
            "SePay webhook rejected: account number mismatch. ProviderTransactionId={ProviderTransactionId}, AccountNumber={AccountNumber}",
            providerTransactionId,
            payload.AccountNumber);
        return false;
    }

    private string? ExtractPaymentCode(SePayWebhookPayloadDto payload, string rawBody)
    {
        var paymentCode = SePayPaymentCodeExtractor.Extract(
            payload.Code,
            payload.Content,
            rawBody,
            _options.PaymentCodeRegex);

        if (!string.IsNullOrWhiteSpace(paymentCode))
        {
            return paymentCode;
        }

        _logger?.LogInformation(
            "SePay webhook ignored: payment code not found. ProviderTransactionId={ProviderTransactionId}",
            payload.Id);
        return null;
    }

    private async Task<bool> IsDuplicateProviderTransactionAsync(
        string providerTransactionId,
        string paymentCode,
        CancellationToken cancellationToken)
    {
        if (!await _payments.ProviderTransactionExistsAsync(PaymentProvider.SEPAY, providerTransactionId, cancellationToken))
        {
            return false;
        }

        _logger?.LogInformation(
            "SePay webhook duplicate ignored. ProviderTransactionId={ProviderTransactionId}, PaymentCode={PaymentCode}",
            providerTransactionId,
            paymentCode);
        return true;
    }

    private async Task<Payment?> LoadCollectablePaymentAsync(
        string paymentCode,
        string providerTransactionId,
        CancellationToken cancellationToken)
    {
        var paymentDetail = await _payments.GetDetailByPaymentCodeAsync(paymentCode, cancellationToken);
        if (paymentDetail is null)
        {
            _logger?.LogWarning(
                "SePay webhook failed: payment not found. ProviderTransactionId={ProviderTransactionId}, PaymentCode={PaymentCode}",
                providerTransactionId,
                paymentCode);
            return null;
        }

        return await _payments.GetByIdAsync(paymentDetail.PaymentId, cancellationToken);
    }

    private bool CanApplyTransferAmount(
        Payment payment,
        SePayWebhookPayloadDto payload,
        string providerTransactionId)
    {
        if (payment.Status is PaymentStatus.CANCELLED or PaymentStatus.EXPIRED or PaymentStatus.REFUNDED)
        {
            _logger?.LogInformation(
                "SePay webhook ignored: payment is not collectable. PaymentId={PaymentId}, Status={Status}",
                payment.PaymentId,
                payment.Status);
            return false;
        }

        if (payload.TransferAmount <= 0m)
        {
            _logger?.LogWarning(
                "SePay webhook failed: transfer amount must be greater than zero. PaymentId={PaymentId}, ProviderTransactionId={ProviderTransactionId}",
                payment.PaymentId,
                providerTransactionId);
            return false;
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
            return false;
        }

        return true;
    }

    private async Task PersistSuccessfulTransferAsync(
        Payment payment,
        SePayWebhookPayloadDto payload,
        string rawBody,
        string providerTransactionId,
        CancellationToken cancellationToken)
    {
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
        PaymentSummaryCalculator.ApplyCharge(payment, appliedAmount, now);

        await PaymentWebhookChargeSupport.CommitSuccessfulChargeAsync(
            _unitOfWork,
            _payments,
            _paymentBusinessEffects,
            payment,
            transaction,
            isExistingTransaction: false,
            cancellationToken);

        _logger?.LogInformation(
            "SePay webhook processed. PaymentId={PaymentId}, ProviderTransactionId={ProviderTransactionId}, AppliedAmount={AppliedAmount}, Status={Status}",
            payment.PaymentId,
            providerTransactionId,
            appliedAmount,
            payment.Status);

        await PaymentWebhookChargeSupport.PushPaymentUpdatedAsync(
            _paymentRealtime,
            _logger,
            payment,
            transaction,
            appliedAmount,
            now,
            cancellationToken);
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
