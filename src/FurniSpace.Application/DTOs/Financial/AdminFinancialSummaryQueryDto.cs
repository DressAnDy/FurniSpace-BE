namespace FurniSpace.Application.DTOs.Financial;

public sealed class AdminFinancialSummaryQueryDto
{
    public string? Period { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public string? Currency { get; set; }
}
