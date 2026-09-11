#nullable enable

using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Financial;

public sealed class AdminFinancialReceivableDetailDto
{
    public AdminFinancialReceivableOrderInfoDto Order { get; set; } = new();
    public AdminFinancialReceivableProjectInfoDto Project { get; set; } = new();
    public AdminFinancialReceivableCustomerInfoDto Customer { get; set; } = new();
    public AdminFinancialReceivableDetailSummaryDto Summary { get; set; } = new();
    public List<AdminFinancialReceivablePaymentRoundDto> PaymentRounds { get; set; } = [];
    public AdminFinancialReceivableActivePaymentDto? ActivePayment { get; set; }
    public string SuggestedAction { get; set; } = string.Empty;
}

public sealed class AdminFinancialReceivableOrderInfoDto
{
    public Guid OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public OrderStatus? OrderStatus { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public decimal FinalTotalAmount { get; set; }
}

public sealed class AdminFinancialReceivableProjectInfoDto
{
    public Guid ProjectId { get; set; }
    public string? ProjectCode { get; set; }
    public string ProjectName { get; set; } = string.Empty;
}

public sealed class AdminFinancialReceivableCustomerInfoDto
{
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
}

public sealed class AdminFinancialReceivableDetailSummaryDto
{
    public decimal FinalTotalAmount { get; set; }
    public decimal? PaidAmount { get; set; }
    public decimal? RemainingAmount { get; set; }
    public decimal PaymentProgressPercentage { get; set; }
    public int ReceivableAgeDays { get; set; }
    public string CollectionState { get; set; } = string.Empty;
    public DateTimeOffset? LastPaidAt { get; set; }
}

public sealed class AdminFinancialReceivablePaymentRoundDto
{
    public Guid? PaymentId { get; set; }
    public string? PaymentCode { get; set; }
    public PaymentType? PaymentType { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public PaymentProvider? Provider { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public DateTimeOffset? ExpiredAt { get; set; }
    public int AttemptCount { get; set; }
    public int FailedAttemptCount { get; set; }
    public string? LastFailureReason { get; set; }
}

public sealed class AdminFinancialReceivableActivePaymentDto
{
    public Guid PaymentId { get; set; }
    public string? PaymentCode { get; set; }
    public PaymentType? PaymentType { get; set; }
    public decimal Amount { get; set; }
    public PaymentStatus? Status { get; set; }
    public DateTimeOffset? ExpiredAt { get; set; }
}
