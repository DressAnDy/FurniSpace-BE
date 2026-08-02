using FurniSpace.Application.DTOs.CustomizationRequests;
using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.ReadModels.CustomizationRequests;
using Mapster;

namespace FurniSpace.Application.Mappings;

public sealed class CustomizationRequestMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<CustomizationRequestVersion, CustomizationRequestVersionDto>()
            .Ignore(dest => dest.IsAccepted)
            .Ignore(dest => dest.ProductVersion);

        config.NewConfig<CustomizationRequestVersionReadModel, CustomizationRequestVersionDto>()
            .Ignore(dest => dest.IsAccepted)
            .Ignore(dest => dest.ProductVersion);
    }
}
