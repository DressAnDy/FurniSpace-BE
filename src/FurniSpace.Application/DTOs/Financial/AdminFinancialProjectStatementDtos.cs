#nullable enable

using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Financial;

public sealed class AdminFinancialProjectStatementQueryDto
{
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public string? EntryType { get; set; }
    public PaymentType? PaymentType { get; set; }
    public string? Status { get; set; }
    public PaymentProvider? Provider { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? SortDirection { get; set; }
}

public sealed class AdminFinancialProjectStatementDto
{
    public AdminFinancialProjectStatementProjectDto Project { get; set; } = new();
    public AdminFinancialProjectStatementSummaryDto Summary { get; set; } = new();
    public List<AdminFinancialProjectStatementItemDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
}

public sealed class AdminFinancialProjectStatementProjectDto
{
    public Guid ProjectId { get; set; }
    public string? ProjectCode { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
}

public sealed class AdminFinancialProjectStatementSummaryDto
{
    public decimal OpeningBalance { get; set; }
    public decimal TotalCollected { get; set; }
    public decimal TotalRefunded { get; set; }
    public decimal NetCollected { get; set; }
    public decimal ClosingBalance { get; set; }
}

public sealed class AdminFinancialProjectStatementItemDto
{
    public Guid EntryId { get; set; }
    public DateTimeOffset? OccurredAt { get; set; }
    public string Direction { get; set; } = string.Empty;
    public string EntryType { get; set; } = string.Empty;
    public string? PaymentType { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? ReferenceCode { get; set; }
    public Guid? OrderId { get; set; }
    public string? OrderCode { get; set; }
    public Guid? PaymentId { get; set; }
    public string? Provider { get; set; }
    public string? Status { get; set; }
    public decimal Amount { get; set; }
    public decimal RunningBalance { get; set; }
}

public static class AdminFinancialStatementDirections
{
    public const string Credit = "CREDIT";
    public const string Debit = "DEBIT";
}

public static class AdminFinancialStatementEntryTypes
{
    public const string Collection = "COLLECTION";
    public const string Refund = "REFUND";
    public const string Adjustment = "ADJUSTMENT";
}
