#nullable enable

using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Orders;

public sealed class UpsertOrderAdjustmentItemDto
{
    public OrderAdjustmentItemType? AdjustmentType { get; set; }
    public Guid? OrderItemId { get; set; }
    public decimal? AdjustmentAmount { get; set; }
    public string? Reason { get; set; }
}
