#nullable enable

using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Financial;

public sealed class AdminFinancialSummaryDrilldownQueryDto
{
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public string? Currency { get; set; }
    public Guid? ProjectId { get; set; }
    public PaymentType? PaymentType { get; set; }
    public string? Status { get; set; }
    public PaymentProvider? Provider { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? SortBy { get; set; }
    public string? SortDirection { get; set; }
}

public sealed class AdminFinancialSummaryDrilldownDto
{
    public string Metric { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int TotalCount { get; set; }
    public string Currency { get; set; } = "VND";
    public AdminFinancialPeriodDto Period { get; set; } = new();
    public IReadOnlyList<AdminFinancialDrilldownBreakdownDto> Breakdowns { get; set; } = [];
    public IReadOnlyList<AdminFinancialDrilldownItemDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
}

public sealed class AdminFinancialDrilldownBreakdownDto
{
    public string Dimension { get; set; } = string.Empty;
    public IReadOnlyList<AdminFinancialDrilldownBreakdownItemDto> Items { get; set; } = [];
}

public sealed class AdminFinancialDrilldownBreakdownItemDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Count { get; set; }
    public decimal Percentage { get; set; }
}

public sealed class AdminFinancialDrilldownItemDto
{
    public string ResourceType { get; set; } = string.Empty;
    public Guid? ProjectId { get; set; }
    public string? ProjectCode { get; set; }
    public string? ProjectName { get; set; }
    public Guid? OrderId { get; set; }
    public string? OrderCode { get; set; }
    public string? OrderStatus { get; set; }
    public Guid? PaymentId { get; set; }
    public string? PaymentCode { get; set; }
    public Guid? TransactionId { get; set; }
    public string? PaymentType { get; set; }
    public string? Status { get; set; }
    public string? Provider { get; set; }
    public decimal Amount { get; set; }
    public decimal? PaidAmount { get; set; }
    public decimal? RemainingAmount { get; set; }
    public DateTimeOffset? OccurredAt { get; set; }
    public DateTimeOffset? ExpiredAt { get; set; }
    public string? FailureReason { get; set; }
    public int AgeDays { get; set; }
}

public static class AdminFinancialSummaryMetrics
{
    public const string Collected = "COLLECTED";
    public const string Outstanding = "OUTSTANDING";
    public const string ContractedReceivable = "CONTRACTED_RECEIVABLE";
    public const string OrderValue = "ORDER_VALUE";
    public const string FailedTransactions = "FAILED_TRANSACTIONS";
    public const string ActivePayments = "ACTIVE_PAYMENTS";
}
