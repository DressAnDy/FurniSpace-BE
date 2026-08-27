using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Repositories.IRepository;

namespace FurniSpace.Infrastructure.Repositories.Repository;

internal static class ProjectScheduleConflictEvaluator
{
    internal const int MinimumGapHours = 2;

    internal readonly record struct ExistingScheduleSlot(
        DateTime ScheduledStart,
        DateTime? ScheduledEnd,
        DateTime? CompletedAt,
        ProjectScheduleStatus? Status);

    internal static StaffScheduleConflictKind Evaluate(
        DateTime scheduledStart,
        DateTime scheduledEnd,
        IEnumerable<ExistingScheduleSlot> existingSchedules)
    {
        var normalizedStart = NormalizeToUtc(scheduledStart);
        var normalizedEnd = NormalizeToUtc(scheduledEnd);

        foreach (var schedule in existingSchedules)
        {
            var existingStart = NormalizeToUtc(schedule.ScheduledStart);
            var existingEnd = GetEffectiveBusyEnd(schedule);

            if (normalizedStart < existingEnd && normalizedEnd > existingStart)
            {
                return StaffScheduleConflictKind.Overlap;
            }

            if (normalizedStart < existingEnd.AddHours(MinimumGapHours) &&
                normalizedEnd.AddHours(MinimumGapHours) > existingStart)
            {
                return StaffScheduleConflictKind.MinimumGapNotMet;
            }
        }

        return StaffScheduleConflictKind.None;
    }

    private static DateTime GetEffectiveBusyEnd(ExistingScheduleSlot schedule)
    {
        if (schedule.Status == ProjectScheduleStatus.COMPLETED && schedule.CompletedAt.HasValue)
        {
            return NormalizeToUtc(schedule.CompletedAt.Value);
        }

        return NormalizeToUtc(schedule.ScheduledEnd ?? schedule.ScheduledStart);
    }

    private static DateTime NormalizeToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}
