using FurniSpace.Application.DTOs.Products;
using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Mapster;

namespace FurniSpace.Application.Mappings;

public sealed class ProductMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Product, ProductDto>();
        config.NewConfig<ProductVersionReadModel, ProductVersionSummaryDto>();
        config.NewConfig<ProductListItemReadModel, ProductListItemDto>();
        config.NewConfig<ProductDetailReadModel, ProductDetailDto>();
    }
}
