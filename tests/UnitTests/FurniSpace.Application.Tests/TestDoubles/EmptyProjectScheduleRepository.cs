#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.ProjectSchedules;
using FurniSpace.Infrastructure.Repositories.IRepository;

namespace FurniSpace.Application.Tests.TestDoubles;

internal sealed class EmptyProjectScheduleRepository : IProjectScheduleRepository
{
    public IQueryable<ProjectSchedule> Query()
    {
        return Enumerable.Empty<ProjectSchedule>().AsQueryable();
    }

    public Task<ProjectSchedule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<ProjectSchedule?>(null);
    }

    public Task<IReadOnlyList<ProjectSchedule>> ListAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<ProjectSchedule>>([]);
    }

    public Task AddAsync(ProjectSchedule entity, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task AddRangeAsync(IEnumerable<ProjectSchedule> entities, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public void Update(ProjectSchedule entity)
    {
    }

    public void Remove(ProjectSchedule entity)
    {
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(0);
    }

    public Task<ProjectScheduleDetailReadModel?> GetDetailAsync(
        Guid scheduleId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<ProjectScheduleDetailReadModel?>(null);
    }

    public Task<(IReadOnlyList<ProjectScheduleListItemReadModel> Items, int Total)> GetListByProjectAsync(
        Guid projectId,
        ProjectScheduleListQueryReadModel query,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<(IReadOnlyList<ProjectScheduleListItemReadModel>, int)>(([], 0));
    }

    public Task<(IReadOnlyList<ProjectScheduleListItemReadModel> Items, int Total)> GetMyAssignedAsync(
        Guid? staffId,
        ProjectScheduleListQueryReadModel query,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<(IReadOnlyList<ProjectScheduleListItemReadModel>, int)>(([], 0));
    }

    public Task<bool> HasCompletedMeasurementScheduleAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    public Task<bool> ExistsMeasurementScheduleAsync(
        Guid projectId,
        ProjectScheduleStatus? status,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    public Task<bool> HasAssignedScheduleAsync(
        Guid projectId,
        Guid staffId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }
}
