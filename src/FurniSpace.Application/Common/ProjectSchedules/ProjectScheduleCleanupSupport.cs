using FurniSpace.Application.Constants.Orders;
using FurniSpace.Application.Constants.ProjectSchedules;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Repositories.IRepository;

namespace FurniSpace.Application.Common.ProjectSchedules;

internal static class ProjectScheduleCleanupSupport
{
    internal static void ApplyMeasurementPhaseSupersededCancellation(
        ProjectSchedule schedule,
        DateTime now)
    {
        if (schedule.ScheduleType != ProjectScheduleType.MEASUREMENT ||
            schedule.Status is ProjectScheduleStatus.COMPLETED or ProjectScheduleStatus.CANCELLED)
        {
            return;
        }

        schedule.Status = ProjectScheduleStatus.CANCELLED;
        schedule.CancelledAt = now;
        schedule.UpdatedAt = now;
        schedule.InternalNote = ProjectScheduleInternalNoteSupport.AppendSystemNote(
            schedule.InternalNote,
            ProjectScheduleCleanupConstants.MeasurementPhaseSupersededNote);
    }

    internal static void ApplyUnusedDeliveryScheduleCancellation(
        ProjectSchedule schedule,
        DateTime now)
    {
        if (schedule.ScheduleType != ProjectScheduleType.DELIVERY ||
            schedule.Status is not (ProjectScheduleStatus.PENDING_CONFIRMATION or ProjectScheduleStatus.CONFIRMED))
        {
            return;
        }

        schedule.Status = ProjectScheduleStatus.CANCELLED;
        schedule.CancelledAt = now;
        schedule.UpdatedAt = now;
        schedule.InternalNote = ProjectScheduleInternalNoteSupport.AppendSystemNote(
            schedule.InternalNote,
            OrderDeliveryConstants.AllItemsAlreadyDeliveredCancellationNote);
    }

    internal static async Task CancelObsoleteMeasurementSchedulesAsync(
        IProjectScheduleRepository schedules,
        Guid projectId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var activeSchedules = await schedules.GetActiveMeasurementSchedulesForCleanupAsync(
            projectId,
            cancellationToken);
        if (activeSchedules.Count == 0)
        {
            return;
        }

        foreach (var schedule in activeSchedules)
        {
            ApplyMeasurementPhaseSupersededCancellation(schedule, now);
            schedules.Update(schedule);
        }
    }

    internal static async Task CancelUnusedDeliverySchedulesAsync(
        IProjectScheduleRepository schedules,
        Guid projectId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var unusedSchedules = await schedules.GetUnusedFutureDeliverySchedulesAsync(
            projectId,
            cancellationToken);
        if (unusedSchedules.Count == 0)
        {
            return;
        }

        foreach (var schedule in unusedSchedules)
        {
            ApplyUnusedDeliveryScheduleCancellation(schedule, now);
            schedules.Update(schedule);
        }
    }
}
