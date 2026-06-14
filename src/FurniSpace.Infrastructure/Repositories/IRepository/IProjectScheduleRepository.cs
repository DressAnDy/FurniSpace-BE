using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.DTOs.ProjectSchedules;
using FurniSpace.Infrastructure.Repositories.Base;

namespace FurniSpace.Infrastructure.Repositories.IRepository;

public interface IProjectScheduleRepository : IGenericRepository<ProjectSchedule>
{
    Task<ProjectScheduleDetailReadModel?> GetDetailAsync(
        Guid scheduleId,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<ProjectScheduleListItemReadModel> Items, int Total)> GetListByProjectAsync(
        Guid projectId,
        ProjectScheduleListQueryReadModel query,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<ProjectScheduleListItemReadModel> Items, int Total)> GetMyAssignedAsync(
        Guid? staffId,
        ProjectScheduleListQueryReadModel query,
        CancellationToken cancellationToken = default);
}
