using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.Financial;

public sealed class AdminFinancialExceptionsQueryReadModel
{
    public string? ExceptionType { get; set; }
    public string? Severity { get; set; }
    public Guid? ProjectId { get; set; }
    public PaymentType? PaymentType { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtcExclusive { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
