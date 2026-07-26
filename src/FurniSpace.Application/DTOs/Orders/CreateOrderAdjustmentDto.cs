#nullable enable

namespace FurniSpace.Application.DTOs.Orders;

public sealed class CreateOrderAdjustmentDto
{
    public string? Reason { get; set; }
    public string? InternalNote { get; set; }
}
