using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Financial;

public sealed class AdminFinancialReceivablesQueryDto
{
    public Guid? ProjectId { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? SalesId { get; set; }
    public PaymentType? PaymentType { get; set; }
    public PaymentStatus? PaymentStatus { get; set; }
    public OrderStatus? OrderStatus { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SortBy { get; set; }
    public string? SortDirection { get; set; }
}
