using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.LayoutAssets;

public sealed class CreateLayoutAssetRequestDto
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public LayoutAssetType AssetType { get; set; }
    public string? Description { get; set; }
}
