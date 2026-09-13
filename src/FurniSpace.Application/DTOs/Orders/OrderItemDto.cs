using FurniSpace.Domain.Common;

namespace FurniSpace.Application.DTOs.Orders;

public sealed class OrderItemDto : OrderItemShape
{
    public int RemainingDeliveryQuantity { get; init; }
}
