using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.Financial;

public sealed class AdminFinancialPaymentsQueryReadModel
{
    public Guid? ProjectId { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? CustomerId { get; set; }
    public PaymentType? PaymentType { get; set; }
    public PaymentStatus? PaymentStatus { get; set; }
    public PaymentProvider? Provider { get; set; }
    public DateTime? CreatedFromUtc { get; set; }
    public DateTime? CreatedToUtcExclusive { get; set; }
    public DateTime? PaidFromUtc { get; set; }
    public DateTime? PaidToUtcExclusive { get; set; }
    public DateTime? ExpiredFromUtc { get; set; }
    public DateTime? ExpiredToUtcExclusive { get; set; }
    public bool? HasFailedAttempt { get; set; }
    public int? MinFailedAttemptCount { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string SortBy { get; set; } = "createdAt";
    public string SortDirection { get; set; } = "desc";
}
