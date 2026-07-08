using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Orders;

public sealed class OrderItemDto
{
    public Guid OrderItemId { get; set; }
    public QuotationItemType? ItemType { get; set; }
    public string? ProductNameSnapshot { get; set; }
    public string? ItemName { get; set; }
    public int? Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? CustomizationAdditionalCost { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? SubtotalAmount { get; set; }
    public bool? IsCustomized { get; set; }
}
