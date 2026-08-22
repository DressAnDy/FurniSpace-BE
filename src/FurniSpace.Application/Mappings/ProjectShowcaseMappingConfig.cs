using FurniSpace.Application.DTOs.ProjectShowcases;
using FurniSpace.Infrastructure.ReadModels.ProjectShowcases;
using Mapster;

namespace FurniSpace.Application.Mappings;

public sealed class ProjectShowcaseMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<ProjectShowcaseDetailReadModel, ProjectShowcaseDto>();
        config.NewConfig<ProjectShowcaseMediaReadModel, ProjectShowcaseMediaDto>();
        config.NewConfig<PublicShowcaseListItemReadModel, PublicShowcaseListItemDto>();
        config.NewConfig<PublicShowcaseDetailReadModel, PublicShowcaseDetailDto>();
        config.NewConfig<PublicShowcaseReviewReadModel, PublicShowcaseReviewDto>();
    }
}
