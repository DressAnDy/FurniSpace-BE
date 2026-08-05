using System;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Domain.Entities;

public class OrderItem
{
    public Guid OrderItemId { get; set; }
    public Guid OrderId { get; set; }
    public Guid? QuotationItemId { get; set; }
    public QuotationItemType ItemType { get; set; }
    public Guid? ProductVersionId { get; set; }
    public string? ProductNameSnapshot { get; set; }
    public string? ProductVersionNameSnapshot { get; set; }
    public string? ProductVersionCodeSnapshot { get; set; }
    public int? Quantity { get; set; }
    public int? DeliveredQuantity { get; set; }
    public OrderItemStatus? Status { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? CustomizationFee { get; set; }
    public decimal? GrossAmount { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? TaxableAmount { get; set; }
    public decimal? TaxRate { get; set; }
    public decimal? TaxAmount { get; set; }
    public decimal? TotalAmount { get; set; }
    public decimal? SubtotalAmount { get; set; }
    public decimal? AdjustmentAmount { get; set; }
    public string? UnavailableReason { get; set; }
    public Guid? UnavailableConfirmedBy { get; set; }
    public DateTime? UnavailableConfirmedAt { get; set; }
    public string? ProductionNote { get; set; }
    public string? DeliveryNote { get; set; }
    public DateTime? LastDeliveredAt { get; set; }
    public Guid? LastDeliveredBy { get; set; }
    public DateTime? CustomerConfirmedAt { get; set; }
}

