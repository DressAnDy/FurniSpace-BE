#nullable enable

using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.Financial;

public sealed class AdminFinancialDiscountQueryReadModel
{
    public DateTime FromUtc { get; set; }
    public DateTime ToUtcExclusive { get; set; }
    public ProjectStatus? ProjectStatus { get; set; }
    public Guid? SalesId { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? OrderId { get; set; }
    public bool? HasDiscount { get; set; }
    public decimal? MinDiscountRate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string SortBy { get; set; } = "confirmedAt";
    public string SortDirection { get; set; } = "desc";
}

public sealed class AdminFinancialDiscountOrderMetricsReadModel
{
    public Guid OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public OrderStatus? OrderStatus { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public Guid ProjectId { get; set; }
    public string? ProjectCode { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public ProjectStatus? ProjectStatus { get; set; }
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public Guid? SalesId { get; set; }
    public string? SalesName { get; set; }
    public decimal GrossOrderValue { get; set; }
    public decimal ItemDiscountAmount { get; set; }
    public decimal TotalDiscountAmount { get; set; }
    public decimal NetOrderValueBeforeVat { get; set; }
    public decimal VatRate { get; set; }
    public decimal VatAmount { get; set; }
    public decimal FinalOrderValue { get; set; }
    public decimal DiscountRate { get; set; }
}

public sealed class AdminFinancialDiscountSummaryReadModel
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
}

public sealed class AdminFinancialDiscountOrderItemReadModel
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

public sealed class AdminFinancialDiscountTrendBucketReadModel
{
    public string Period { get; set; } = string.Empty;
    public DateTime PeriodStartUtc { get; set; }
    public decimal GrossOrderValue { get; set; }
    public decimal TotalDiscountAmount { get; set; }
    public decimal DiscountRate { get; set; }
    public int DiscountedOrderCount { get; set; }
    public int TotalOrderCount { get; set; }
}

public sealed class AdminFinancialDiscountExceptionReadModel
{
    public string ExceptionType { get; set; } = string.Empty;
    public AdminFinancialDiscountOrderMetricsReadModel Order { get; set; } = new();
    public decimal ThresholdRate { get; set; }
    public decimal ThresholdAmount { get; set; }
}
