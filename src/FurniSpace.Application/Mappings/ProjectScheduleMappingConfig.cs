using FurniSpace.Application.DTOs.ProjectSchedules;
using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.ReadModels.ProjectSchedules;
using Mapster;

namespace FurniSpace.Application.Mappings;

public sealed class ProjectScheduleMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<ProjectSchedule, ProjectScheduleDto>();
        config.NewConfig<ProjectScheduleDetailReadModel, ProjectScheduleDto>();
        config.NewConfig<ProjectScheduleListItemReadModel, ProjectScheduleDto>();
    }
}
