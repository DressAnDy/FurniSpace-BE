#nullable enable

using System;
using FurniSpace.Application.Common.ProjectSchedules;
using FurniSpace.Application.Constants.ProjectSchedules;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using Xunit;

namespace FurniSpace.Application.Tests.ProjectSchedules;

public sealed class ProjectScheduleCleanupSupportTests
{
    [Fact]
    public void ApplyMeasurementPhaseSupersededCancellation_SetsCancelledAndAppendsNote()
    {
        var schedule = CreateMeasurementSchedule(ProjectScheduleStatus.CONFIRMED, "Existing note");
        var now = new DateTime(2026, 9, 13, 10, 0, 0, DateTimeKind.Utc);

        ProjectScheduleCleanupSupport.ApplyMeasurementPhaseSupersededCancellation(schedule, now);

        Assert.Equal(ProjectScheduleStatus.CANCELLED, schedule.Status);
        Assert.Equal(now, schedule.CancelledAt);
        Assert.Equal(now, schedule.UpdatedAt);
        Assert.Contains("Existing note", schedule.InternalNote, StringComparison.Ordinal);
        Assert.Contains(ProjectScheduleCleanupConstants.MeasurementPhaseSupersededNote, schedule.InternalNote, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyMeasurementPhaseSupersededCancellation_WhenCompleted_DoesNotMutate()
    {
        var schedule = CreateMeasurementSchedule(ProjectScheduleStatus.COMPLETED, null);
        var originalUpdatedAt = schedule.UpdatedAt;

        ProjectScheduleCleanupSupport.ApplyMeasurementPhaseSupersededCancellation(
            schedule,
            DateTime.UtcNow);

        Assert.Equal(ProjectScheduleStatus.COMPLETED, schedule.Status);
        Assert.Equal(originalUpdatedAt, schedule.UpdatedAt);
    }

    [Fact]
    public void AppendSystemNote_WhenNoteAlreadyContainsSystemNote_IsIdempotent()
    {
        var existing = $"Audit{Environment.NewLine}{ProjectScheduleCleanupConstants.MeasurementPhaseSupersededNote}";

        var result = ProjectScheduleInternalNoteSupport.AppendSystemNote(
            existing,
            ProjectScheduleCleanupConstants.MeasurementPhaseSupersededNote);

        Assert.Equal(existing, result);
    }

    private static ProjectSchedule CreateMeasurementSchedule(
        ProjectScheduleStatus status,
        string? internalNote)
    {
        return new ProjectSchedule
        {
            ScheduleId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            ScheduleType = ProjectScheduleType.MEASUREMENT,
            Status = status,
            ScheduledStart = DateTime.UtcNow,
            InternalNote = internalNote,
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };
    }
}
