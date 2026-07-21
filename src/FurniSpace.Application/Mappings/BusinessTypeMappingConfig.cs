using FurniSpace.Application.DTOs.BusinessTypes;
using FurniSpace.Domain.Entities;
using Mapster;

namespace FurniSpace.Application.Mappings;

public sealed class BusinessTypeMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<BusinessType, BusinessTypeDto>();
    }
}
