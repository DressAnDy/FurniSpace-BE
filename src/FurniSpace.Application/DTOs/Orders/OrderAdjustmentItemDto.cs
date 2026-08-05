#nullable enable

namespace FurniSpace.Application.DTOs.Orders;

public sealed class OrderAdjustmentItemDto
{
    public Guid OrderAdjustmentItemId { get; set; }
    public Guid OrderAdjustmentId { get; set; }
    public Guid? OrderItemId { get; set; }
    public string AdjustmentType { get; set; } = string.Empty;
    public decimal PreviousItemAmount { get; set; }
    public decimal AdjustmentAmount { get; set; }
    public decimal? ItemTotalAmount { get; set; }
    public string Reason { get; set; } = string.Empty;
}
