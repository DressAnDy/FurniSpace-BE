using System;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Domain.Entities;

public class Order
{
    public Guid OrderId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? ProposalId { get; set; }
    public Guid QuotationId { get; set; }
    public string OrderCode { get; set; } = null!;
    public Guid CustomerId { get; set; }
    public Guid? SalesId { get; set; }
    public decimal OriginalTotalAmount { get; set; }
    public decimal? ItemAdjustmentAmount { get; set; }
    public decimal? AdditionalDiscountAmount { get; set; }
    public decimal FinalTotalAmount { get; set; }
    public decimal? DepositAmount { get; set; }
    public decimal? PaidAmount { get; set; }
    public decimal? RemainingAmount { get; set; }
    public OrderStatus? Status { get; set; }
    public string? DeliveryAddress { get; set; }
    public string? ReceiverName { get; set; }
    public string? ReceiverPhone { get; set; }
    public string? DeliveryNote { get; set; }
    public string? CustomerDeliveryNote { get; set; }
    public DateTime? CustomerConfirmedDeliveryAt { get; set; }
    public Guid? ConfirmedBy { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

