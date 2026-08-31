#nullable enable

using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Financial;

public static class AdminFinancialDiscountErrorCodes
{
    public const string DateRangeInvalid = "FINANCIAL_DISCOUNT_DATE_RANGE_INVALID";
    public const string FilterInvalid = "FINANCIAL_DISCOUNT_FILTER_INVALID";
    public const string GranularityInvalid = "FINANCIAL_DISCOUNT_GRANULARITY_INVALID";
    public const string OrderNotFound = "FINANCIAL_DISCOUNT_ORDER_NOT_FOUND";
}

public static class AdminFinancialDiscountExceptionTypes
{
    public const string HighDiscountRate = "HIGH_DISCOUNT_RATE";
    public const string HighDiscountAmount = "HIGH_DISCOUNT_AMOUNT";
}

public sealed class AdminFinancialDiscountSummaryQueryDto
{
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public string? Currency { get; set; }
    public ProjectStatus? ProjectStatus { get; set; }
    public Guid? SalesId { get; set; }
    public Guid? CustomerId { get; set; }
}

public sealed class AdminFinancialDiscountSummaryDto
{
    public decimal GrossOrderValue { get; set; }
    public decimal ItemDiscountAmount { get; set; }
    public decimal TotalDiscountAmount { get; set; }
    public decimal NetOrderValueBeforeVat { get; set; }
    public decimal VatAmount { get; set; }
    public decimal FinalOrderValue { get; set; }
    public decimal AverageDiscountRate { get; set; }
    public int DiscountedOrderCount { get; set; }
    public int TotalOrderCount { get; set; }
    public DateTimeOffset? PeriodFrom { get; set; }
    public DateTimeOffset? PeriodTo { get; set; }
    public string Currency { get; set; } = "VND";
}

public sealed class AdminFinancialDiscountProjectsQueryDto
{
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? SalesId { get; set; }
    public bool? HasDiscount { get; set; }
    public decimal? MinDiscountRate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SortBy { get; set; }
    public string? SortDirection { get; set; }
}

public sealed class AdminFinancialDiscountProjectsDto
{
    public List<AdminFinancialDiscountProjectRowDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
}

public sealed class AdminFinancialDiscountProjectRowDto
{
    public Guid ProjectId { get; set; }
    public string? ProjectCode { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public ProjectStatus? ProjectStatus { get; set; }
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public Guid? SalesId { get; set; }
    public string? SalesName { get; set; }
    public Guid OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public OrderStatus? OrderStatus { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public decimal GrossOrderValue { get; set; }
    public decimal ItemDiscountAmount { get; set; }
    public decimal TotalDiscountAmount { get; set; }
    public decimal NetOrderValueBeforeVat { get; set; }
    public decimal VatAmount { get; set; }
    public decimal FinalOrderValue { get; set; }
    public decimal DiscountRate { get; set; }
}

public sealed class AdminFinancialDiscountOrderDetailDto
{
    public Guid OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public OrderStatus? OrderStatus { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public Guid ProjectId { get; set; }
    public string? ProjectCode { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public decimal GrossOrderValue { get; set; }
    public decimal ItemDiscountAmount { get; set; }
    public decimal TotalDiscountAmount { get; set; }
    public decimal NetOrderValueBeforeVat { get; set; }
    public decimal VatRate { get; set; }
    public decimal VatAmount { get; set; }
    public decimal FinalOrderValue { get; set; }
    public decimal DiscountRate { get; set; }
    public List<AdminFinancialDiscountOrderItemDto> Items { get; set; } = [];
}

public sealed class AdminFinancialDiscountOrderItemDto
{
    public Guid OrderItemId { get; set; }
    public string? ProductName { get; set; }
    public string? ProductVersionName { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineGrossAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal SubtotalAmount { get; set; }
}

public sealed class AdminFinancialDiscountTrendQueryDto
{
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public string? Granularity { get; set; }
    public string? Currency { get; set; }
    public Guid? SalesId { get; set; }
}

public sealed class AdminFinancialDiscountTrendDto
{
    public string Granularity { get; set; } = "MONTH";
    public string Timezone { get; set; } = "Asia/Ho_Chi_Minh";
    public string Currency { get; set; } = "VND";
    public List<AdminFinancialDiscountTrendBucketDto> Series { get; set; } = [];
}

public sealed class AdminFinancialDiscountTrendBucketDto
{
    public string Period { get; set; } = string.Empty;
    public DateTimeOffset PeriodStart { get; set; }
    public decimal GrossOrderValue { get; set; }
    public decimal TotalDiscountAmount { get; set; }
    public decimal DiscountRate { get; set; }
    public int DiscountedOrderCount { get; set; }
    public int TotalOrderCount { get; set; }
}

public sealed class AdminFinancialDiscountExceptionsQueryDto
{
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public decimal? ThresholdRate { get; set; }
    public decimal? ThresholdAmount { get; set; }
    public Guid? SalesId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed class AdminFinancialDiscountExceptionsDto
{
    public List<AdminFinancialDiscountExceptionRowDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
}

public sealed class AdminFinancialDiscountExceptionRowDto
{
    public string ExceptionType { get; set; } = string.Empty;
    public Guid OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public Guid ProjectId { get; set; }
    public string? ProjectCode { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public Guid? SalesId { get; set; }
    public string? SalesName { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public decimal GrossOrderValue { get; set; }
    public decimal TotalDiscountAmount { get; set; }
    public decimal DiscountRate { get; set; }
    public decimal FinalOrderValue { get; set; }
    public decimal ThresholdRate { get; set; }
    public decimal ThresholdAmount { get; set; }
}
