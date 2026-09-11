using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.LayoutAssets;

public sealed class UpdateLayoutAssetStatusRequestDto
{
    public LayoutAssetStatus Status { get; set; }
}
