using FurniSpace.Application.DTOs.ProjectAreas;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.ProjectAreas;
using Mapster;

namespace FurniSpace.Application.Mappings;

public sealed class ProjectAreaMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<ProjectArea, ProjectAreaDto>();
        config.NewConfig<ProjectAreaDetailReadModel, ProjectAreaDto>();

        config.NewConfig<CreateProjectAreaRequestDto, ProjectArea>()
            .Map(dest => dest.AreaName, src => src.AreaName!.Trim())
            .Map(dest => dest.Status, src => src.Status ?? ProjectAreaStatus.DRAFT)
            .AfterMapping((src, dest) =>
            {
                dest.Description = NormalizeOptional(src.Description);
                dest.CurrentCondition = NormalizeOptional(src.CurrentCondition);
                dest.RequirementNote = NormalizeOptional(src.RequirementNote);
            })
            .Ignore(
                nameof(ProjectArea.ProjectAreaId),
                nameof(ProjectArea.ProjectId),
                nameof(ProjectArea.CreatedBy),
                nameof(ProjectArea.CreatedAt),
                nameof(ProjectArea.UpdatedAt));

        config.NewConfig<UpdateProjectAreaRequestDto, ProjectArea>()
            .IgnoreNullValues(true)
            .Ignore(
                nameof(ProjectArea.AreaName),
                nameof(ProjectArea.Description),
                nameof(ProjectArea.CurrentCondition),
                nameof(ProjectArea.RequirementNote))
            .AfterMapping(ApplyUpdateStringFields);
    }

    private static void ApplyUpdateStringFields(UpdateProjectAreaRequestDto src, ProjectArea dest)
    {
        if (!string.IsNullOrWhiteSpace(src.AreaName))
        {
            dest.AreaName = src.AreaName.Trim();
        }

        if (src.Description != null)
        {
            dest.Description = NormalizeOptional(src.Description);
        }

        if (src.CurrentCondition != null)
        {
            dest.CurrentCondition = NormalizeOptional(src.CurrentCondition);
        }

        if (src.RequirementNote != null)
        {
            dest.RequirementNote = NormalizeOptional(src.RequirementNote);
        }
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
