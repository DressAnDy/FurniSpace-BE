using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Financial;

public sealed class AdminFinancialPaymentRowDto
{
    public Guid PaymentId { get; set; }
    public string PaymentCode { get; set; } = string.Empty;
    public Guid ProjectId { get; set; }
    public string? ProjectCode { get; set; }
    public Guid? OrderId { get; set; }
    public string? OrderCode { get; set; }
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public PaymentType? PaymentType { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public PaymentStatus? Status { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? ExpiredAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public PaymentProvider? LastProvider { get; set; }
    public int AttemptCount { get; set; }
    public int FailedAttemptCount { get; set; }
    public PaymentTransactionStatus? LastTransactionStatus { get; set; }
    public string? LastFailureReason { get; set; }
    public DateTime? LastAttemptAt { get; set; }
}
