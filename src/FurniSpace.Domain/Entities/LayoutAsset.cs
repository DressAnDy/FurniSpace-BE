using System;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Domain.Entities;

public class LayoutAsset
{
    public Guid LayoutAssetId { get; set; }
    public string AssetCode { get; set; } = null!;
    public string AssetName { get; set; } = null!;
    public LayoutAssetType AssetType { get; set; }
    public string? Description { get; set; }
    public LayoutAssetStatus Status { get; set; } = LayoutAssetStatus.ACTIVE;
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
