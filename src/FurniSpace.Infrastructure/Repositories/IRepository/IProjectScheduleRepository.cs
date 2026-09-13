using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.ProjectSchedules;
using FurniSpace.Infrastructure.Repositories.Base;

namespace FurniSpace.Infrastructure.Repositories.IRepository;

public enum StaffScheduleConflictKind
{
    None,
    Overlap,
    MinimumGapNotMet
}

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

    Task<bool> HasCompletedMeasurementScheduleAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsMeasurementScheduleAsync(
        Guid projectId,
        ProjectScheduleStatus? status,
        CancellationToken cancellationToken = default);

    Task<bool> HasAssignedScheduleAsync(
        Guid projectId,
        Guid staffId,
        CancellationToken cancellationToken = default);

    Task<bool> HasConfirmedDeliveryScheduleAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    Task<bool> HasActiveDeliveryScheduleAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    Task<DateOnly?> GetMaxOperationalScheduleDateAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<DateOnly?>(null);
    }

    Task<bool> HasActiveStaffOverlapAsync(
        Guid assignedStaffId,
        DateTime scheduledStart,
        DateTime? scheduledEnd,
        Guid? excludedScheduleId = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    Task<StaffScheduleConflictKind> GetStaffScheduleConflictAsync(
        Guid assignedStaffId,
        DateTime scheduledStart,
        DateTime scheduledEnd,
        Guid? excludedScheduleId = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(StaffScheduleConflictKind.None);
    }

    Task<StaffScheduleConflictKind> GetCustomerScheduleConflictAsync(
        Guid customerId,
        DateTime scheduledStart,
        DateTime scheduledEnd,
        Guid? excludedScheduleId = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(StaffScheduleConflictKind.None);
    }

    Task<IReadOnlyList<ProjectSchedule>> GetUnusedFutureDeliverySchedulesAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<ProjectSchedule>>([]);
    }

    Task<bool> HasUnresolvedConfirmedDeliveryScheduleAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    Task<bool> HasLinkedInProgressDeliveryAsync(
        Guid scheduleId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }
}
