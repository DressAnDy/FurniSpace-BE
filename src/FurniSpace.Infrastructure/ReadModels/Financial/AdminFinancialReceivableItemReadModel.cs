#nullable enable

using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.Financial;

public sealed class AdminFinancialReceivableItemReadModel
{
    public Guid ProjectId { get; set; }
    public string? ProjectCode { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public Guid OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public OrderStatus? OrderStatus { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public decimal FinalTotalAmount { get; set; }
    public decimal? PaidAmount { get; set; }
    public decimal? RemainingAmount { get; set; }
    public decimal PaymentProgressPercentage { get; set; }
    public string CollectionState { get; set; } = string.Empty;
    public int ReceivableAgeDays { get; set; }
    public DateTime? LastPaidAt { get; set; }
    public Guid? ActivePaymentId { get; set; }
    public PaymentType? ActivePaymentType { get; set; }
    public decimal? ActivePaymentAmount { get; set; }
    public PaymentStatus? ActivePaymentStatus { get; set; }
    public DateTime? ActivePaymentExpiredAt { get; set; }
    public string? LastPaymentFailureReason { get; set; }
}

public sealed class AdminFinancialReceivableDetailReadModel
{
    public Guid OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public OrderStatus? OrderStatus { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public decimal FinalTotalAmount { get; set; }
    public decimal? PaidAmount { get; set; }
    public decimal? RemainingAmount { get; set; }
    public Guid ProjectId { get; set; }
    public string? ProjectCode { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string CollectionState { get; set; } = string.Empty;
    public int ReceivableAgeDays { get; set; }
    public decimal PaymentProgressPercentage { get; set; }
    public DateTime? LastPaidAt { get; set; }
    public Guid? ActivePaymentId { get; set; }
    public string? ActivePaymentCode { get; set; }
    public PaymentType? ActivePaymentType { get; set; }
    public decimal? ActivePaymentAmount { get; set; }
    public PaymentStatus? ActivePaymentStatus { get; set; }
    public DateTime? ActivePaymentExpiredAt { get; set; }
    public IReadOnlyList<AdminFinancialReceivablePaymentRoundReadModel> PaymentRounds { get; set; } = [];
}

public sealed class AdminFinancialReceivablePaymentRoundReadModel
{
    public Guid? PaymentId { get; set; }
    public string? PaymentCode { get; set; }
    public PaymentType? PaymentType { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public PaymentProvider? Provider { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? ExpiredAt { get; set; }
    public int AttemptCount { get; set; }
    public int FailedAttemptCount { get; set; }
    public string? LastFailureReason { get; set; }
}
