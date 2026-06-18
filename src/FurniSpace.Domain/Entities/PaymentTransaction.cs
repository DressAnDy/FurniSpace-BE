using System;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Domain.Entities;

public class PaymentTransaction
{
    public Guid PaymentTransactionId { get; set; }
    public Guid PaymentId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? OrderId { get; set; }
    public string TransactionCode { get; set; } = string.Empty;
    public PaymentTransactionType? TransactionType { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public string? PaymentMethod { get; set; }
    public string? ProviderTransactionId { get; set; }
    public PaymentTransactionStatus? Status { get; set; }
    public DateTime? TransactionTime { get; set; }
    public Guid? ConfirmedBy { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public string? FailureReason { get; set; }
    public string? RawProviderPayload { get; set; }
    public DateTime? CreatedAt { get; set; }
}
