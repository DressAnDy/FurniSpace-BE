namespace FurniSpace.Application.DTOs.Orders;

public sealed class OrderListResponseDto
{
    public IReadOnlyList<OrderListItemDto> Items { get; init; } = [];
}
