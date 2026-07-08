using FurniSpace.Domain.Common;

namespace FurniSpace.Application.DTOs.Orders;

public sealed class OrderDetailDto : OrderDetailShape
{
    public IReadOnlyList<OrderItemDto> Items { get; init; } = [];
}
