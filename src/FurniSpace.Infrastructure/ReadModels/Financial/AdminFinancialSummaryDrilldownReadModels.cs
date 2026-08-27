#nullable enable

using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.Financial;

public sealed class AdminFinancialSummaryDrilldownQueryReadModel
{
    public string Metric { get; init; } = string.Empty;
    public Guid? ProjectId { get; init; }
    public PaymentType? PaymentType { get; init; }
    public string? Status { get; init; }
    public PaymentProvider? Provider { get; init; }
    public string? GroupBy { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public string SortBy { get; init; } = "occurredAt";
    public string SortDirection { get; init; } = "desc";
}

public sealed class AdminFinancialSummaryDrilldownReadModel
{
    public string Metric { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public int TotalCount { get; init; }
    public IReadOnlyList<AdminFinancialDrilldownBreakdownReadModel> Breakdowns { get; init; } = [];
    public IReadOnlyList<AdminFinancialDrilldownItemReadModel> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
}

public sealed class AdminFinancialDrilldownBreakdownReadModel
{
    public string Dimension { get; init; } = string.Empty;
    public IReadOnlyList<AdminFinancialDrilldownBreakdownItemReadModel> Items { get; init; } = [];
}

public sealed class AdminFinancialDrilldownBreakdownItemReadModel
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public int Count { get; init; }
}

public sealed class AdminFinancialDrilldownItemReadModel
{
    public string ResourceType { get; set; } = string.Empty;
    public Guid? ProjectId { get; set; }
    public string? ProjectCode { get; set; }
    public string? ProjectName { get; set; }
    public Guid? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public Guid? OrderId { get; set; }
    public string? OrderCode { get; set; }
    public string? OrderStatus { get; set; }
    public decimal? OrderFinalTotal { get; set; }
    public decimal? OrderPaidAmount { get; set; }
    public decimal? OrderRemainingAmount { get; set; }
    public Guid? PaymentId { get; set; }
    public string? PaymentCode { get; set; }
    public Guid? TransactionId { get; set; }
    public string? PaymentType { get; set; }
    public string? Status { get; set; }
    public string? Provider { get; set; }
    public decimal Amount { get; set; }
    public decimal? PaidAmount { get; set; }
    public decimal? RemainingAmount { get; set; }
    public DateTime? OccurredAt { get; set; }
    public DateTime? ExpiredAt { get; set; }
    public string? FailureReason { get; set; }
    public int AgeDays { get; set; }
    public decimal? ProjectStartFeeAmount { get; set; }
    public decimal? DepositAmount { get; set; }
    public decimal? RemainingPaymentAmount { get; set; }
    public decimal? FullPaymentAmount { get; set; }
    public decimal? TotalCollectedAmount { get; set; }
    public int? PaymentCount { get; set; }
    public DateTime? LastPaidAt { get; set; }
}
