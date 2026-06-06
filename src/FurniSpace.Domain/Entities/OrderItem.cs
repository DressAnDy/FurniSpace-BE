using System;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Domain.Entities;

public class OrderItem
{
    public Guid OrderItemId { get; set; }
    public Guid OrderId { get; set; }
    public Guid? QuotationItemId { get; set; }
    public Guid? ProductVersionId { get; set; }
    public string? ProductNameSnapshot { get; set; }
    public string? ProductVersionNameSnapshot { get; set; }
    public string? ProductVersionCodeSnapshot { get; set; }
    public int? Quantity { get; set; }
    public int? DeliveredQuantity { get; set; }
    public OrderItemStatus? Status { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? CustomizationFee { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? SubtotalAmount { get; set; }
    public decimal? AdjustmentAmount { get; set; }
    public string? UnavailableReason { get; set; }
    public string? ProductionNote { get; set; }
    public string? DeliveryNote { get; set; }
    public DateTime? LastDeliveredAt { get; set; }
    public Guid? LastDeliveredBy { get; set; }
    public DateTime? CustomerConfirmedAt { get; set; }
}

