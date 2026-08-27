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
    public string ResourceType { get; init; } = string.Empty;
    public Guid? ProjectId { get; init; }
    public string? ProjectCode { get; init; }
    public string? ProjectName { get; init; }
    public Guid? OrderId { get; init; }
    public string? OrderCode { get; init; }
    public string? OrderStatus { get; init; }
    public Guid? PaymentId { get; init; }
    public string? PaymentCode { get; init; }
    public Guid? TransactionId { get; init; }
    public string? PaymentType { get; init; }
    public string? Status { get; init; }
    public string? Provider { get; init; }
    public decimal Amount { get; init; }
    public decimal? PaidAmount { get; init; }
    public decimal? RemainingAmount { get; init; }
    public DateTime? OccurredAt { get; init; }
    public DateTime? ExpiredAt { get; init; }
    public string? FailureReason { get; init; }
    public int AgeDays { get; init; }
}
