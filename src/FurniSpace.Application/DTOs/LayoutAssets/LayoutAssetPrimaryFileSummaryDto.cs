namespace FurniSpace.Application.DTOs.LayoutAssets;

public sealed class LayoutAssetPrimaryFileSummaryDto
{
    public Guid FileId { get; set; }
    public string Url { get; set; } = string.Empty;
}
