namespace FurniSpace.Domain.Enums;

public enum OrderStatus
{
    CREATED,
    DEPOSIT_PENDING,
    DEPOSIT_PAID,
    IN_PRODUCTION,
    READY_FOR_DELIVERY,
    DELIVERING,
    DELIVERED,
    FINAL_PAYMENT_PENDING,
    COMPLETED,
    CANCELLED
}
