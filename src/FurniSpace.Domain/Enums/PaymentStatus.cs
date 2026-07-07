namespace FurniSpace.Domain.Enums;

public enum PaymentStatus
{
    PENDING,
    PROCESSING,
    PARTIALLY_PAID,
    PAID,
    FAILED,
    CANCELLED,
    EXPIRED,
    REFUNDED
}
