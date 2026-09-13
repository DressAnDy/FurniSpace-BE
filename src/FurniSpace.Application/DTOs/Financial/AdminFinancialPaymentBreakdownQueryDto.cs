namespace FurniSpace.Application.DTOs.Financial;

public sealed class AdminFinancialPaymentBreakdownQueryDto
{
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public string? Currency { get; set; }
}
