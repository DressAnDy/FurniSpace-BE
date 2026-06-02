using System;

namespace FurniSpace.Domain.Entities;

public class Payment
{
    public Guid PaymentId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? QuotationId { get; set; }
    public Guid? PaidBy { get; set; }
    public string? PaymentType { get; set; }
    public decimal Amount { get; set; }
    public string? PaymentMethod { get; set; }
    public string? TransactionReference { get; set; }
    public string? Status { get; set; }
    public DateOnly? DueDate { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? Note { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}


