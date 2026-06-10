using FurniSpace.Application.DTOs.Products;
using FurniSpace.Application.DTOs.ProductVersions;
using FurniSpace.Application.DTOs.Projects;
using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.DTOs.Products;
using FurniSpace.Infrastructure.DTOs.Projects;
using Mapster;

namespace FurniSpace.Application.Mappings;

public sealed class ProductMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Product, ProductDto>();
        config.NewConfig<ProductVersion, ProductVersionDto>();
        config.NewConfig<Project, ProjectDto>();
        config.NewConfig<Project, ProjectSalesAssignmentDto>();
        config.NewConfig<Project, ProjectBasicInformationDto>();
        config.NewConfig<Project, ProjectStatusUpdateDto>();
        config.NewConfig<Project, ProjectRejectionDto>();
        config.NewConfig<Project, ProjectInformationRequestDto>()
            .Map(destination => destination.RequestedAt, source => source.UpdatedAt ?? default);
        config.NewConfig<ProjectDetailReadModel, ProjectDto>();
        config.NewConfig<ProjectListQueryDto, ProjectListQueryReadModel>();
        config.NewConfig<ProjectListItemReadModel, ProjectListItemDto>();
        config.NewConfig<ProductVersionReadModel, ProductVersionSummaryDto>();
        config.NewConfig<ProductListItemReadModel, ProductListItemDto>();
        config.NewConfig<ProductDetailReadModel, ProductDetailDto>();
    }
}
