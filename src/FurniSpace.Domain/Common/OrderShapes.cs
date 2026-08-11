using System;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Domain.Common;

public abstract class OrderItemShape
{
    public Guid OrderItemId { get; set; }
    public string? ProductNameSnapshot { get; set; }
    public string? ItemName { get; set; }
    public int? Quantity { get; set; }
    public OrderItemStatus? Status { get; set; }
    public int? DeliveredQuantity { get; set; }
    public DateTime? CustomerConfirmedAt { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? SubtotalAmount { get; set; }
    public bool? IsCustomized { get; set; }
}

public abstract class OrderListItemShape
{
    public Guid OrderId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid QuotationId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public decimal OriginalTotalAmount { get; set; }
    public decimal? DepositAmount { get; set; }
    public decimal? PaidAmount { get; set; }
    public decimal? RemainingAmount { get; set; }
    public OrderStatus? Status { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public abstract class OrderDetailShape
{
    public Guid OrderId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? ProposalId { get; set; }
    public Guid QuotationId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public Guid? SalesId { get; set; }
    public decimal VatRate { get; set; }
    public decimal VatAmount { get; set; }
    public decimal OriginalTotalAmount { get; set; }
    public decimal? ItemAdjustmentAmount { get; set; }
    public decimal? AdditionalDiscountAmount { get; set; }
    public decimal FinalTotalAmount { get; set; }
    public decimal? DepositAmount { get; set; }
    public decimal? PaidAmount { get; set; }
    public decimal? RemainingAmount { get; set; }
    public OrderStatus? Status { get; set; }
}
