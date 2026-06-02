using System;

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
    public decimal TotalAmount { get; set; }
    public decimal? PaidAmount { get; set; }
    public decimal? RemainingAmount { get; set; }
    public string? Status { get; set; }
    public Guid? ConfirmedBy { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}


