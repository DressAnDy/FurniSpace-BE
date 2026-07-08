using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.Common.Payments;

internal static class PaymentSummaryCalculator
{
    internal static void ApplyCharge(Payment payment, decimal appliedAmount, DateTime occurredAt)
    {
        payment.PaidAmount += appliedAmount;
        payment.RemainingAmount = Math.Max(0m, payment.Amount - payment.PaidAmount);
        payment.Status = payment.RemainingAmount <= 0m
            ? PaymentStatus.PAID
            : PaymentStatus.PARTIALLY_PAID;
        payment.PaidAt = payment.Status == PaymentStatus.PAID ? occurredAt : payment.PaidAt;
        payment.UpdatedAt = occurredAt;
    }
}
