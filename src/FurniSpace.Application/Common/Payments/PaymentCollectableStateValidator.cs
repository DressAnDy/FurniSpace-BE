using FurniSpace.Application.DTOs.Payments;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.Common.Payments;

internal static class PaymentCollectableStateValidator
{
    private static readonly PaymentStatus[] CollectableStatuses =
    [
        PaymentStatus.PENDING,
        PaymentStatus.PROCESSING,
        PaymentStatus.PARTIALLY_PAID
    ];

    internal static PaymentStateValidationResult Validate(Payment payment)
    {
        if (payment.Status is PaymentStatus.CANCELLED or PaymentStatus.PAID or PaymentStatus.EXPIRED or PaymentStatus.REFUNDED)
        {
            return PaymentStateValidationResult.Invalid(
                PaymentErrorCodes.InvalidPaymentStatus,
                "Payment is not eligible for collection.");
        }

        if (!payment.Status.HasValue || !CollectableStatuses.Contains(payment.Status.Value))
        {
            return PaymentStateValidationResult.Invalid(
                PaymentErrorCodes.InvalidPaymentStatus,
                "Payment is not eligible for collection.");
        }

        if (payment.ExpiredAt.HasValue && payment.ExpiredAt.Value <= DateTime.UtcNow)
        {
            return PaymentStateValidationResult.Invalid(
                PaymentErrorCodes.PaymentExpired,
                "Payment has expired.");
        }

        if (payment.RemainingAmount <= 0m)
        {
            return PaymentStateValidationResult.Invalid(
                PaymentErrorCodes.InvalidPaymentAmount,
                "Payment has no remaining amount.");
        }

        return PaymentStateValidationResult.Valid();
    }
}

internal sealed class PaymentStateValidationResult
{
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public bool IsValid => ErrorCode is null;

    public static PaymentStateValidationResult Valid()
    {
        return new PaymentStateValidationResult();
    }

    public static PaymentStateValidationResult Invalid(string errorCode, string errorMessage)
    {
        return new PaymentStateValidationResult
        {
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
    }
}
