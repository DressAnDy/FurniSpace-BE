using FurniSpace.Application.DTOs.LayoutAssets;
using FurniSpace.Domain.Entities;
using Mapster;

namespace FurniSpace.Application.Mappings;

public sealed class LayoutAssetMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<LayoutAsset, LayoutAssetDto>()
            .Map(dest => dest.Files, _ => Array.Empty<LayoutAssetFileDto>());
    }
}
