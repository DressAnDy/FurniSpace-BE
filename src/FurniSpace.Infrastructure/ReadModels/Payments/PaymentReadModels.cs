using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.Payments;

public sealed class PaymentDetailReadModel
{
    public Guid PaymentId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? QuotationId { get; set; }
    public string PaymentCode { get; set; } = string.Empty;
    public Guid? PaidBy { get; set; }
    public PaymentType? PaymentType { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public PaymentStatus? Status { get; set; }
    public DateTime? ExpiredAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? Note { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid CustomerId { get; set; }
    public Guid? AssignedSalesId { get; set; }
    public Guid? AssignedDesignerId { get; set; }
}

public sealed class PaymentListItemReadModel
{
    public Guid PaymentId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? PaidBy { get; set; }
    public string PaymentCode { get; set; } = string.Empty;
    public string? ProjectCode { get; set; }
    public string? ProjectName { get; set; }
    public string? OrderCode { get; set; }
    public PaymentType? PaymentType { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public PaymentStatus? Status { get; set; }
    public DateTime? ExpiredAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public sealed class PaymentTransactionReadModel
{
    public Guid PaymentTransactionId { get; set; }
    public Guid PaymentId { get; set; }
    public string TransactionCode { get; set; } = string.Empty;
    public PaymentTransactionType? TransactionType { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public PaymentProvider? PaymentProvider { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public string? ProviderTransactionId { get; set; }
    public string? ProviderReferenceCode { get; set; }
    public PaymentTransactionStatus? Status { get; set; }
    public string? PaymentUrl { get; set; }
    public string? QrContent { get; set; }
    public string? FailureReason { get; set; }
    public DateTime? TransactionTime { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public sealed class PaymentQueryReadModel
{
    public Guid? ProjectId { get; set; }
    public Guid? OrderId { get; set; }
    public PaymentStatus? Status { get; set; }
    public PaymentType? PaymentType { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public Guid AccessUserId { get; set; }
    public string? AccessRole { get; set; }
}

public sealed class PaymentSummaryReadModel
{
    public int PendingCount { get; set; }
    public int ProcessingCount { get; set; }
    public int PaidCount { get; set; }
    public int ExpiredCount { get; set; }
    public int CancelledCount { get; set; }
    public int RefundedCount { get; set; }
    public decimal PayablePendingAmount { get; set; }
    public int PayableCount { get; set; }
}

public sealed class PaymentStatusByCodeReadModel
{
    public Guid PaymentId { get; set; }
    public string PaymentCode { get; set; } = string.Empty;
    public PaymentStatus? Status { get; set; }
    public decimal Amount { get; set; }
    public DateTime? PaidAt { get; set; }
}
