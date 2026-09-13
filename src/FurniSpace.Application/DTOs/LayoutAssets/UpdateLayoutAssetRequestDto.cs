using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.LayoutAssets;

public sealed class UpdateLayoutAssetRequestDto
{
    public string AssetName { get; set; } = string.Empty;
    public LayoutAssetType AssetType { get; set; }
    public string? Description { get; set; }
}
