using FurniSpace.Application.DTOs.LayoutAssets;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.RoomPlanner;

public sealed class RoomPlannerResolvedLayoutAssetDto
{
    public Guid LayoutAssetId { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public LayoutAssetType AssetType { get; set; }
    public string? Description { get; set; }
    public LayoutAssetStatus Status { get; set; }
    public IReadOnlyList<LayoutAssetFileDto> Files { get; set; } = [];
    public LayoutAssetPrimaryFileSummaryDto? PrimaryModel { get; set; }
    public LayoutAssetPrimaryFileSummaryDto? PrimaryTexture { get; set; }
    public LayoutAssetPrimaryFileSummaryDto? PrimaryPreview { get; set; }
}
