using System;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Domain.Entities;

public class Payment
{
    public Guid PaymentId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? QuotationId { get; set; }
    public string PaymentCode { get; set; } = string.Empty;
    public Guid? PaidBy { get; set; }
    public PaymentType? PaymentType { get; set; }
    public decimal Amount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public string Currency { get; set; } = "VND";
    public PaymentStatus? Status { get; set; }
    public DateTime? ExpiredAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? Note { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
