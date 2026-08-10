using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Financial;

public sealed class AdminFinancialPaymentsQueryDto
{
    public Guid? ProjectId { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? CustomerId { get; set; }
    public PaymentType? PaymentType { get; set; }
    public PaymentStatus? PaymentStatus { get; set; }
    public PaymentProvider? Provider { get; set; }
    public DateTimeOffset? CreatedFrom { get; set; }
    public DateTimeOffset? CreatedTo { get; set; }
    public DateTimeOffset? PaidFrom { get; set; }
    public DateTimeOffset? PaidTo { get; set; }
    public DateTimeOffset? ExpiredFrom { get; set; }
    public DateTimeOffset? ExpiredTo { get; set; }
    public bool? HasFailedAttempt { get; set; }
    public int? MinFailedAttemptCount { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SortBy { get; set; }
    public string? SortDirection { get; set; }
}
