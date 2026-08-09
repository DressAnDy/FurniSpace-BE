using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.Financial;

public sealed class AdminFinancialProjectsQueryReadModel
{
    public Guid? ProjectId { get; set; }
    public string? Keyword { get; set; }
    public ProjectStatus? ProjectStatus { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? SalesId { get; set; }
    public PaymentStatus? PaymentStatus { get; set; }
    public PaymentType? PaymentType { get; set; }
    public bool? HasOrder { get; set; }
    public bool? HasOutstandingPayment { get; set; }
    public bool? HasReceivable { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtcExclusive { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string SortBy { get; set; } = "createdAt";
    public string SortDirection { get; set; } = "desc";
}
