namespace FurniSpace.Application.DTOs.Orders;

public sealed class CreateDeliveryBatchItemRequestDto
{
    public Guid OrderItemId { get; init; }
    public int Quantity { get; init; }
    public string? Note { get; init; }
}

public sealed class CreateDeliveryBatchRequestDto
{
    public string? Note { get; init; }
    public IReadOnlyList<CreateDeliveryBatchItemRequestDto> Items { get; init; } = [];
}
