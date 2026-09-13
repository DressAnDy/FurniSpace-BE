namespace FurniSpace.Application.DTOs.Financial;

public sealed class AdminFinancialCollectionTrendQueryDto
{
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public string? Granularity { get; set; }
    public string? Currency { get; set; }
}
