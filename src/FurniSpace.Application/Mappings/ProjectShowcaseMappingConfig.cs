using FurniSpace.Application.Common.ProjectShowcases;
using FurniSpace.Application.DTOs.ProjectShowcases;
using FurniSpace.Infrastructure.ReadModels.ProjectShowcases;
using Mapster;

namespace FurniSpace.Application.Mappings;

public sealed class ProjectShowcaseMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<ProjectShowcaseDetailReadModel, ProjectShowcaseDto>()
            .Map(dest => dest.Introduction, src => src.Description)
            .Map(dest => dest.CoverUrl, src => ResolveCoverUrl(src.Media));
        config.NewConfig<ProjectShowcaseMediaReadModel, ProjectShowcaseMediaDto>();
        config.NewConfig<PublicShowcaseReviewReadModel, PublicShowcaseReviewDto>();
        config.NewConfig<AdminProjectShowcaseListItemReadModel, AdminProjectShowcaseListItemDto>()
            .Map(dest => dest.Introduction, src => src.Description);

        config.NewConfig<PublicShowcaseListItemReadModel, PublicShowcaseListItemDto>()
            .Map(dest => dest.Introduction, src => src.Description)
            .Map(dest => dest.CompletedDate, src => PublicShowcaseProjectProjection.ToCompletedDate(src.CompletedAt))
            .Map(dest => dest.TotalAreaSqm, src => src.TotalAreaSqm);

        config.NewConfig<PublicShowcaseDetailReadModel, PublicShowcaseDetailDto>()
            .Map(dest => dest.Introduction, src => src.Description)
            .Map(dest => dest.CompletedDate, src => PublicShowcaseProjectProjection.ToCompletedDate(src.CompletedAt))
            .Map(dest => dest.CompletionYear, src => PublicShowcaseProjectProjection.ToCompletionYear(src.CompletedAt))
            .Map(
                dest => dest.ImplementationDurationDays,
                src => PublicShowcaseProjectProjection.ToImplementationDurationDays(src.SubmittedAt, src.CompletedAt))
            .Map(dest => dest.CoverUrl, src => ResolveCoverUrl(src.Media));
    }

    private static string? ResolveCoverUrl(IReadOnlyList<ProjectShowcaseMediaReadModel> media)
    {
        return media.FirstOrDefault(item => item.IsCover)?.FileUrl;
    }
}
