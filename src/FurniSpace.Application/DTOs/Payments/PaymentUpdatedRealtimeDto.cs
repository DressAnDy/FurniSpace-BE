using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Payments;

public sealed class PaymentUpdatedRealtimeDto
{
    public Guid PaymentId { get; set; }
    public Guid ProjectId { get; set; }
    public string PaymentCode { get; set; } = string.Empty;
    public PaymentStatus? Status { get; set; }
    public decimal Amount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public Guid PaymentTransactionId { get; set; }
    public decimal TransactionAmount { get; set; }
    public decimal AppliedAmount { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime OccurredAt { get; set; }
}
