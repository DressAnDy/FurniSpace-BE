#nullable enable

using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.Financial;

public sealed class AdminFinancialReceivablesQueryReadModel
{
    public string? Keyword { get; set; }
    public string? CollectionState { get; set; }
    public int? MinAgeDays { get; set; }
    public int? MaxAgeDays { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? SalesId { get; set; }
    public PaymentType? PaymentType { get; set; }
    public PaymentStatus? PaymentStatus { get; set; }
    public OrderStatus? OrderStatus { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtcExclusive { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string SortBy { get; set; } = "confirmedAt";
    public string SortDirection { get; set; } = "desc";
}
