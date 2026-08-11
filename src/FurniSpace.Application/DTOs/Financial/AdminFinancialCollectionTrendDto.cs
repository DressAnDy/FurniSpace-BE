namespace FurniSpace.Application.DTOs.Financial;

public sealed class AdminFinancialCollectionTrendDto
{
    public string Granularity { get; set; } = string.Empty;
    public string Timezone { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public List<AdminFinancialCollectionTrendBucketDto> Series { get; set; } = [];
}
