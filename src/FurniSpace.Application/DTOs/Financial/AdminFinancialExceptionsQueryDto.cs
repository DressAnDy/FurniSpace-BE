using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Financial;

public sealed class AdminFinancialExceptionsQueryDto
{
    public string? ExceptionType { get; set; }
    public string? Severity { get; set; }
    public Guid? ProjectId { get; set; }
    public PaymentType? PaymentType { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
