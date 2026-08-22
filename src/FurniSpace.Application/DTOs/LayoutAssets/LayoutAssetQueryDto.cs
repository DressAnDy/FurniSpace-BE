using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.LayoutAssets;

public sealed class LayoutAssetQueryDto
{
    public LayoutAssetType? AssetType { get; set; }
    public LayoutAssetStatus? Status { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
