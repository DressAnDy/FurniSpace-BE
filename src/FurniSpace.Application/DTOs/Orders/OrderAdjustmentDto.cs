#nullable enable

namespace FurniSpace.Application.DTOs.Orders;

public sealed class OrderAdjustmentDto
{
    public Guid OrderAdjustmentId { get; set; }
    public Guid OrderId { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal ItemAdjustmentAmount { get; set; }
    public decimal AdditionalDiscountAmount { get; set; }
    public decimal TotalAdjustmentAmount { get; set; }
}
