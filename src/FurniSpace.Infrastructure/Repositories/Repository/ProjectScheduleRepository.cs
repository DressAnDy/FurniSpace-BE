using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.DTOs.ProjectSchedules;
using FurniSpace.Infrastructure.Repositories.Base;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Infrastructure.Repositories.Repository;

public sealed class ProjectScheduleRepository : GenericRepository<ProjectSchedule>, IProjectScheduleRepository
{
    public ProjectScheduleRepository(AppDbContext dbContext) : base(dbContext)
    {
    }

    public Task<ProjectScheduleDetailReadModel?> GetDetailAsync(
        Guid scheduleId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.ProjectScheduleSet
            .Where(s => s.ScheduleId == scheduleId)
            .Join(
                DbContext.ProjectSet,
                s => s.ProjectId,
                p => p.ProjectId,
                (s, p) => new ProjectScheduleDetailReadModel
                {
                    ScheduleId = s.ScheduleId,
                    ProjectId = s.ProjectId,
                    ProjectName = p.ProjectName ?? string.Empty,
                    CustomerId = p.CustomerId,
                    AssignedSalesId = p.AssignedSalesId,
                    AssignedDesignerId = p.AssignedDesignerId,
                    ProjectAreaId = s.ProjectAreaId,
                    CreatedBy = s.CreatedBy,
                    AssignedStaffId = s.AssignedStaffId,
                    ScheduleType = s.ScheduleType,
                    Title = s.Title,
                    Description = s.Description,
                    ScheduledStart = s.ScheduledStart,
                    ScheduledEnd = s.ScheduledEnd,
                    Location = s.Location,
                    Status = s.Status,
                    CustomerNote = s.CustomerNote,
                    InternalNote = s.InternalNote,
                    CreatedAt = s.CreatedAt,
                    UpdatedAt = s.UpdatedAt,
                    CancelledAt = s.CancelledAt
                })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<ProjectScheduleListItemReadModel> Items, int Total)> GetListByProjectAsync(
        Guid projectId,
        ProjectScheduleListQueryReadModel query,
        CancellationToken cancellationToken = default)
    {
        var scheduleQuery = DbContext.ProjectScheduleSet
            .Where(s => s.ProjectId == projectId);

        scheduleQuery = ApplyFilters(scheduleQuery, query);

        return await GetPagedListAsync(scheduleQuery, query, cancellationToken);
    }

    public async Task<(IReadOnlyList<ProjectScheduleListItemReadModel> Items, int Total)> GetMyAssignedAsync(
        Guid? staffId,
        ProjectScheduleListQueryReadModel query,
        CancellationToken cancellationToken = default)
    {
        var scheduleQuery = DbContext.ProjectScheduleSet.AsQueryable();

        if (staffId.HasValue)
        {
            var id = staffId.Value;
            scheduleQuery = scheduleQuery.Where(s => s.AssignedStaffId == id);
        }

        scheduleQuery = ApplyFilters(scheduleQuery, query);

        return await GetPagedListAsync(scheduleQuery, query, cancellationToken);
    }

    private async Task<(IReadOnlyList<ProjectScheduleListItemReadModel> Items, int Total)> GetPagedListAsync(
        IQueryable<ProjectSchedule> scheduleQuery,
        ProjectScheduleListQueryReadModel query,
        CancellationToken cancellationToken)
    {
        var total = await scheduleQuery.CountAsync(cancellationToken);

        var items = await scheduleQuery
            .OrderByDescending(s => s.ScheduledStart)
            .Skip((query.Page - 1) * query.Limit)
            .Take(query.Limit)
            .Join(
                DbContext.ProjectSet,
                s => s.ProjectId,
                p => p.ProjectId,
                (s, p) => new ProjectScheduleListItemReadModel
                {
                    ScheduleId = s.ScheduleId,
                    ProjectId = s.ProjectId,
                    ProjectAreaId = s.ProjectAreaId,
                    CustomerId = p.CustomerId,
                    AssignedSalesId = p.AssignedSalesId,
                    AssignedDesignerId = p.AssignedDesignerId,
                    AssignedStaffId = s.AssignedStaffId,
                    CreatedBy = s.CreatedBy,
                    ScheduleType = s.ScheduleType,
                    Title = s.Title,
                    ScheduledStart = s.ScheduledStart,
                    ScheduledEnd = s.ScheduledEnd,
                    Location = s.Location,
                    Status = s.Status,
                    CreatedAt = s.CreatedAt
                })
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    private static IQueryable<ProjectSchedule> ApplyFilters(
        IQueryable<ProjectSchedule> query,
        ProjectScheduleListQueryReadModel filter)
    {
        if (filter.ScheduleType.HasValue)
        {
            var type = filter.ScheduleType.Value;
            query = query.Where(s => s.ScheduleType == type);
        }

        if (filter.Status.HasValue)
        {
            var status = filter.Status.Value;
            query = query.Where(s => s.Status == status);
        }

        if (filter.From.HasValue)
        {
            var from = filter.From.Value;
            query = query.Where(s => s.ScheduledStart >= from);
        }

        if (filter.To.HasValue)
        {
            var to = filter.To.Value;
            query = query.Where(s => s.ScheduledStart <= to);
        }

        return query;
    }
}
