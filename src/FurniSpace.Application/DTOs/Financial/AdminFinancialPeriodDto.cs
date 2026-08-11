namespace FurniSpace.Application.DTOs.Financial;

public sealed class AdminFinancialPeriodDto
{
    public string Type { get; set; } = string.Empty;
    public DateTimeOffset From { get; set; }
    public DateTimeOffset To { get; set; }
    public string Timezone { get; set; } = string.Empty;
}
