using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.Common.Payments;

internal static class PaymentPayableEvaluator
{
    internal static bool IsPayable(Payment payment, bool hasSuccessfulTransaction, DateTime utcNow)
    {
        if (hasSuccessfulTransaction || payment.Amount <= 0m)
        {
            return false;
        }

        return ActivePaymentResolver.IsActive(payment, utcNow);
    }

    internal static bool IsPayable(
        PaymentStatus? status,
        decimal amount,
        DateTime? expiredAt,
        bool hasSuccessfulTransaction,
        DateTime utcNow)
    {
        if (hasSuccessfulTransaction || amount <= 0m || !status.HasValue)
        {
            return false;
        }

        if (!ActivePaymentResolver.ActiveStatuses.Contains(status.Value))
        {
            return false;
        }

        return !expiredAt.HasValue || expiredAt.Value > utcNow;
    }
}
