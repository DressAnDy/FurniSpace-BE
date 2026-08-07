using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.Common.Payments;

internal static class ActivePaymentResolver
{
    internal static readonly PaymentStatus[] ActiveStatuses =
    [
        PaymentStatus.PENDING,
        PaymentStatus.PROCESSING
    ];

    internal static bool IsExpired(Payment payment, DateTime utcNow)
    {
        return payment.ExpiredAt.HasValue && payment.ExpiredAt.Value <= utcNow;
    }

    internal static bool IsActive(Payment payment, DateTime utcNow)
    {
        return payment.Status.HasValue &&
            ActiveStatuses.Contains(payment.Status.Value) &&
            !IsExpired(payment, utcNow);
    }

    internal static void MarkExpired(Payment payment, DateTime utcNow)
    {
        payment.Status = PaymentStatus.EXPIRED;
        payment.UpdatedAt = utcNow;
    }
}
