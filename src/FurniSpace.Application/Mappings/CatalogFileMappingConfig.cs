using FurniSpace.Application.DTOs.Products;
using FurniSpace.Infrastructure.DTOs.Products;
using Mapster;

namespace FurniSpace.Application.Mappings;

public sealed class CatalogFileMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<CatalogFileReadModel, CatalogFileDto>();
    }
}
