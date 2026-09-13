#nullable enable

using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.Financial;

public sealed class AdminFinancialProjectStatementQueryReadModel
{
    public Guid ProjectId { get; init; }
    public DateTime FromUtc { get; init; }
    public DateTime ToUtcExclusive { get; init; }
    public string? EntryType { get; init; }
    public PaymentType? PaymentType { get; init; }
    public string? Status { get; init; }
    public PaymentProvider? Provider { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public string SortDirection { get; init; } = "desc";
}

public sealed class AdminFinancialProjectStatementReadModel
{
    public Guid ProjectId { get; init; }
    public string? ProjectCode { get; init; }
    public string ProjectName { get; init; } = string.Empty;
    public string? CustomerName { get; init; }
    public decimal OpeningBalance { get; init; }
    public decimal TotalCollected { get; init; }
    public decimal TotalRefunded { get; init; }
    public decimal NetCollected { get; init; }
    public decimal ClosingBalance { get; init; }
    public IReadOnlyList<AdminFinancialProjectStatementItemReadModel> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
}

public sealed record AdminFinancialProjectStatementItemReadModel
{
    public Guid EntryId { get; init; }
    public DateTime? OccurredAt { get; init; }
    public string Direction { get; init; } = string.Empty;
    public string EntryType { get; init; } = string.Empty;
    public string? PaymentType { get; init; }
    public string Description { get; init; } = string.Empty;
    public string? ReferenceCode { get; init; }
    public Guid? OrderId { get; init; }
    public string? OrderCode { get; init; }
    public Guid? PaymentId { get; init; }
    public string? Provider { get; init; }
    public string? Status { get; init; }
    public decimal Amount { get; init; }
    public decimal SignedAmount { get; init; }
    public decimal RunningBalance { get; init; }
}
