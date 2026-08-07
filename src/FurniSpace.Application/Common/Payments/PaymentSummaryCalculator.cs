using FurniSpace.Application.DTOs.Payments;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.Common.Payments;

internal static class PaymentSummaryCalculator
{
    internal static bool TryApplySuccessfulCharge(
        Payment payment,
        decimal transactionAmount,
        string transactionCurrency,
        DateTime occurredAt,
        out string? errorCode)
    {
        if (payment.Status == PaymentStatus.PAID)
        {
            errorCode = PaymentErrorCodes.PaymentAlreadyPaid;
            return false;
        }

        if (transactionAmount != payment.Amount)
        {
            errorCode = PaymentErrorCodes.PaymentAmountMismatch;
            return false;
        }

        if (!string.Equals(transactionCurrency, payment.Currency, StringComparison.OrdinalIgnoreCase))
        {
            errorCode = PaymentErrorCodes.PaymentCurrencyMismatch;
            return false;
        }

        payment.Status = PaymentStatus.PAID;
        payment.PaidAt = occurredAt;
        payment.UpdatedAt = occurredAt;
        errorCode = null;
        return true;
    }

    internal static void MarkProcessing(Payment payment, DateTime occurredAt)
    {
        if (payment.Status == PaymentStatus.PENDING)
        {
            payment.Status = PaymentStatus.PROCESSING;
            payment.UpdatedAt = occurredAt;
        }
    }

    internal static void RevertToPendingIfCollectable(Payment payment, DateTime occurredAt)
    {
        if (payment.Status != PaymentStatus.PROCESSING || ActivePaymentResolver.IsExpired(payment, occurredAt))
        {
            return;
        }

        payment.Status = PaymentStatus.PENDING;
        payment.UpdatedAt = occurredAt;
    }
}
