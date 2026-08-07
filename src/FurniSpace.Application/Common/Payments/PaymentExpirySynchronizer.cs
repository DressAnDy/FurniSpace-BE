using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.Common.Payments;

internal static class PaymentExpirySynchronizer
{
    internal static bool TryMarkExpiredIfNeeded(Payment payment, DateTime utcNow)
    {
        if (payment.Status is PaymentStatus.PAID or PaymentStatus.CANCELLED or PaymentStatus.REFUNDED or PaymentStatus.EXPIRED)
        {
            return false;
        }

        if (!ActivePaymentResolver.IsExpired(payment, utcNow))
        {
            return false;
        }

        ActivePaymentResolver.MarkExpired(payment, utcNow);
        return true;
    }
}
