using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.ReadModels.ProjectSchedules;
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
                    CancelledAt = s.CancelledAt,
                    CompletedAt = s.CompletedAt
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
                    CreatedAt = s.CreatedAt,
                    CompletedAt = s.CompletedAt
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

    public Task<bool> HasCompletedMeasurementScheduleAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return ExistsMeasurementScheduleAsync(
            projectId,
            ProjectScheduleStatus.COMPLETED,
            cancellationToken);
    }

    public Task<bool> ExistsMeasurementScheduleAsync(
        Guid projectId,
        ProjectScheduleStatus? status,
        CancellationToken cancellationToken = default)
    {
        var query = DbContext.ProjectScheduleSet
            .Where(s =>
                s.ProjectId == projectId &&
                s.ScheduleType == ProjectScheduleType.MEASUREMENT &&
                s.Status != ProjectScheduleStatus.CANCELLED);

        if (status.HasValue)
        {
            query = query.Where(s => s.Status == status.Value);
        }

        return query.AnyAsync(cancellationToken);
    }

    public Task<bool> HasAssignedScheduleAsync(
        Guid projectId,
        Guid staffId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.ProjectScheduleSet.AnyAsync(
            schedule =>
                schedule.ProjectId == projectId &&
                schedule.AssignedStaffId == staffId,
            cancellationToken);
    }

    public Task<bool> HasConfirmedDeliveryScheduleAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.ProjectScheduleSet.AnyAsync(
            schedule =>
                schedule.ProjectId == projectId &&
                schedule.ScheduleType == ProjectScheduleType.DELIVERY &&
                schedule.Status == ProjectScheduleStatus.CONFIRMED,
            cancellationToken);
    }

    public Task<bool> HasActiveDeliveryScheduleAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.ProjectScheduleSet.AnyAsync(
            schedule =>
                schedule.ProjectId == projectId &&
                schedule.ScheduleType == ProjectScheduleType.DELIVERY &&
                (schedule.Status == ProjectScheduleStatus.PENDING_CONFIRMATION ||
                 schedule.Status == ProjectScheduleStatus.CONFIRMED),
            cancellationToken);
    }

    public async Task<DateOnly?> GetMaxOperationalScheduleDateAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var schedules = await DbContext.ProjectScheduleSet
            .AsNoTracking()
            .Where(schedule =>
                schedule.ProjectId == projectId &&
                schedule.Status != ProjectScheduleStatus.CANCELLED)
            .Select(schedule => new { schedule.ScheduledStart, schedule.ScheduledEnd })
            .ToListAsync(cancellationToken);

        DateOnly? maxDate = null;
        foreach (var schedule in schedules)
        {
            maxDate = MaxDateOnly(maxDate, DateOnly.FromDateTime(schedule.ScheduledStart.ToUniversalTime()));
            if (schedule.ScheduledEnd.HasValue)
            {
                maxDate = MaxDateOnly(
                    maxDate,
                    DateOnly.FromDateTime(schedule.ScheduledEnd.Value.ToUniversalTime()));
            }
        }

        return maxDate;
    }

    public Task<bool> HasActiveStaffOverlapAsync(
        Guid assignedStaffId,
        DateTime scheduledStart,
        DateTime? scheduledEnd,
        Guid? excludedScheduleId = null,
        CancellationToken cancellationToken = default)
    {
        var newEnd = scheduledEnd ?? scheduledStart;
        var query = DbContext.ProjectScheduleSet
            .Where(schedule =>
                schedule.AssignedStaffId == assignedStaffId &&
                schedule.Status != ProjectScheduleStatus.CANCELLED &&
                (schedule.Status == ProjectScheduleStatus.PENDING_CONFIRMATION ||
                 schedule.Status == ProjectScheduleStatus.CONFIRMED ||
                 (schedule.Status == ProjectScheduleStatus.COMPLETED && schedule.CompletedAt != null)));

        if (excludedScheduleId.HasValue)
        {
            var scheduleId = excludedScheduleId.Value;
            query = query.Where(schedule => schedule.ScheduleId != scheduleId);
        }

        return query.AnyAsync(
            schedule =>
                scheduledStart < (schedule.Status == ProjectScheduleStatus.COMPLETED
                    ? schedule.CompletedAt!.Value
                    : (schedule.ScheduledEnd ?? schedule.ScheduledStart)) &&
                newEnd > schedule.ScheduledStart,
            cancellationToken);
    }

    public async Task<StaffScheduleConflictKind> GetStaffScheduleConflictAsync(
        Guid assignedStaffId,
        DateTime scheduledStart,
        DateTime scheduledEnd,
        Guid? excludedScheduleId = null,
        CancellationToken cancellationToken = default)
    {
        var query = ApplyAppointmentConflictFilters(
            DbContext.ProjectScheduleSet.Where(schedule => schedule.AssignedStaffId == assignedStaffId),
            excludedScheduleId);

        var schedules = await query
            .Select(schedule => new ProjectScheduleConflictEvaluator.ExistingScheduleSlot(
                schedule.ScheduledStart,
                schedule.ScheduledEnd,
                schedule.CompletedAt,
                schedule.Status))
            .ToListAsync(cancellationToken);

        return ProjectScheduleConflictEvaluator.Evaluate(scheduledStart, scheduledEnd, schedules);
    }

    public async Task<StaffScheduleConflictKind> GetCustomerScheduleConflictAsync(
        Guid customerId,
        DateTime scheduledStart,
        DateTime scheduledEnd,
        Guid? excludedScheduleId = null,
        CancellationToken cancellationToken = default)
    {
        var query = ApplyAppointmentConflictFilters(
            DbContext.ProjectScheduleSet,
            excludedScheduleId);

        var schedules = await query
            .Join(
                DbContext.ProjectSet,
                schedule => schedule.ProjectId,
                project => project.ProjectId,
                (schedule, project) => new { schedule, project })
            .Where(item => item.project.CustomerId == customerId)
            .Select(item => new ProjectScheduleConflictEvaluator.ExistingScheduleSlot(
                item.schedule.ScheduledStart,
                item.schedule.ScheduledEnd,
                item.schedule.CompletedAt,
                item.schedule.Status))
            .ToListAsync(cancellationToken);

        return ProjectScheduleConflictEvaluator.Evaluate(scheduledStart, scheduledEnd, schedules);
    }

    private static IQueryable<ProjectSchedule> ApplyAppointmentConflictFilters(
        IQueryable<ProjectSchedule> query,
        Guid? excludedScheduleId)
    {
        query = query.Where(schedule =>
            schedule.Status != ProjectScheduleStatus.CANCELLED &&
            (schedule.ScheduleType == ProjectScheduleType.MEASUREMENT ||
             schedule.ScheduleType == ProjectScheduleType.DELIVERY) &&
            (schedule.Status == ProjectScheduleStatus.PENDING_CONFIRMATION ||
             schedule.Status == ProjectScheduleStatus.CONFIRMED ||
             (schedule.Status == ProjectScheduleStatus.COMPLETED && schedule.CompletedAt != null)) &&
            (schedule.Status == ProjectScheduleStatus.COMPLETED || schedule.ScheduledEnd != null));

        if (excludedScheduleId.HasValue)
        {
            var scheduleId = excludedScheduleId.Value;
            query = query.Where(schedule => schedule.ScheduleId != scheduleId);
        }

        return query;
    }

    private static DateOnly? MaxDateOnly(DateOnly? current, DateOnly candidate)
    {
        return !current.HasValue || candidate > current.Value
            ? candidate
            : current;
    }

    public async Task<IReadOnlyList<ProjectSchedule>> GetUnusedFutureDeliverySchedulesAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var schedules = await DbContext.ProjectScheduleSet
            .Where(schedule =>
                schedule.ProjectId == projectId &&
                schedule.ScheduleType == ProjectScheduleType.DELIVERY &&
                (schedule.Status == ProjectScheduleStatus.PENDING_CONFIRMATION ||
                 schedule.Status == ProjectScheduleStatus.CONFIRMED))
            .ToListAsync(cancellationToken);

        var scheduleIds = schedules.Select(schedule => schedule.ScheduleId).ToList();
        if (scheduleIds.Count == 0)
        {
            return [];
        }

        var linkedScheduleIds = await DbContext.DeliverySet
            .Where(delivery =>
                delivery.ProjectScheduleId.HasValue &&
                scheduleIds.Contains(delivery.ProjectScheduleId.Value))
            .Select(delivery => delivery.ProjectScheduleId!.Value)
            .ToListAsync(cancellationToken);

        return schedules
            .Where(schedule => !linkedScheduleIds.Contains(schedule.ScheduleId))
            .ToList();
    }

    public Task<bool> HasUnresolvedConfirmedDeliveryScheduleAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.ProjectScheduleSet.AnyAsync(
            schedule =>
                schedule.ProjectId == projectId &&
                schedule.ScheduleType == ProjectScheduleType.DELIVERY &&
                schedule.Status == ProjectScheduleStatus.CONFIRMED &&
                !DbContext.DeliverySet.Any(delivery =>
                    delivery.ProjectScheduleId == schedule.ScheduleId &&
                    delivery.Status == DeliveryStatus.COMPLETED),
            cancellationToken);
    }

    public Task<bool> HasLinkedInProgressDeliveryAsync(
        Guid scheduleId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.DeliverySet.AnyAsync(
            delivery =>
                delivery.ProjectScheduleId == scheduleId &&
                delivery.Status == DeliveryStatus.IN_PROGRESS,
            cancellationToken);
    }
}
