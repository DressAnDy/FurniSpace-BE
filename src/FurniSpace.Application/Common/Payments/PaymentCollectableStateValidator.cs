using FurniSpace.Application.DTOs.Payments;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.Common.Payments;

internal static class PaymentCollectableStateValidator
{
    internal static PaymentStateValidationResult Validate(Payment payment)
    {
        if (payment.Status is PaymentStatus.CANCELLED or PaymentStatus.PAID or PaymentStatus.EXPIRED or PaymentStatus.REFUNDED)
        {
            return PaymentStateValidationResult.Invalid(
                PaymentErrorCodes.PaymentNotPayable,
                "Payment is not eligible for collection.");
        }

        if (!payment.Status.HasValue || !ActivePaymentResolver.ActiveStatuses.Contains(payment.Status.Value))
        {
            return PaymentStateValidationResult.Invalid(
                PaymentErrorCodes.PaymentNotPayable,
                "Payment is not eligible for collection.");
        }

        if (ActivePaymentResolver.IsExpired(payment, DateTime.UtcNow))
        {
            return PaymentStateValidationResult.Invalid(
                PaymentErrorCodes.PaymentExpired,
                "Payment has expired.");
        }

        if (payment.Amount <= 0m)
        {
            return PaymentStateValidationResult.Invalid(
                PaymentErrorCodes.InvalidPaymentAmount,
                "Payment amount must be greater than zero.");
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
