#nullable enable

using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Financial;

public sealed class AdminFinancialReceivablesQueryDto
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
    public DateTimeOffset? ConfirmedFrom { get; set; }
    public DateTimeOffset? ConfirmedTo { get; set; }
    /// <summary>Alias of <see cref="ConfirmedFrom"/> for backward compatibility.</summary>
    public DateTimeOffset? From { get; set; }
    /// <summary>Alias of <see cref="ConfirmedTo"/> for backward compatibility.</summary>
    public DateTimeOffset? To { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SortBy { get; set; }
    public string? SortDirection { get; set; }
}
