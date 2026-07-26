using System;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Domain.Entities;

public class OrderAdjustment
{
    public Guid OrderAdjustmentId { get; set; }
    public Guid OrderId { get; set; }
    public OrderAdjustmentStatus Status { get; set; } = OrderAdjustmentStatus.DRAFT;
    public decimal ItemAdjustmentAmount { get; set; }
    public decimal AdditionalDiscountAmount { get; set; }
    public decimal TotalAdjustmentAmount { get; set; }
    public string Reason { get; set; } = null!;
    public string? InternalNote { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? ConfirmedBy { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public Guid? AppliedBy { get; set; }
    public DateTime? AppliedAt { get; set; }
    public Guid? CancelledBy { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
}
