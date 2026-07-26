using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.Common.Orders;

internal static class OrderPaidAmountRecalculator
{
    internal static readonly PaymentType[] OrderScopedPaymentTypes =
    [
        PaymentType.DEPOSIT,
        PaymentType.REMAINING_PAYMENT,
        PaymentType.FULL_PAYMENT
    ];

    internal static readonly PaymentStatus[] CountablePaymentStatuses =
    [
        PaymentStatus.PAID
    ];

    internal static (decimal PaidAmount, decimal RemainingAmount) Calculate(
        decimal finalTotalAmount,
        decimal summedPaidAmount)
    {
        var paidAmount = Math.Max(0m, summedPaidAmount);
        var remainingAmount = Math.Max(0m, finalTotalAmount - paidAmount);
        return (paidAmount, remainingAmount);
    }
}
