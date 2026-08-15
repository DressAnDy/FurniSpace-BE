using System;
using FurniSpace.Application.Common.Projects;
using FurniSpace.Application.DTOs.ProjectSchedules;
using FurniSpace.Application.DTOs.Projects;
using Xunit;

namespace FurniSpace.Application.Tests.Projects;

public sealed class ProjectTimelineDateValidatorTests
{
    [Fact]
    public void ValidateTargetNotInPast_WhenDateIsPast_ReturnsValidationError()
    {
        var today = new DateOnly(2026, 8, 15);
        var target = new DateOnly(2026, 8, 14);

        var error = ProjectTimelineDateValidator.ValidateTargetNotInPast(target, today);

        Assert.NotNull(error);
        Assert.Equal(ProjectErrorCodes.InvalidTargetCompletionDate, error!.Code);
    }

    [Fact]
    public void ValidateTargetNotInPast_WhenDateIsToday_ReturnsNull()
    {
        var today = new DateOnly(2026, 8, 15);

        var error = ProjectTimelineDateValidator.ValidateTargetNotInPast(today, today);

        Assert.Null(error);
    }

    [Fact]
    public void ValidateScheduleDateWithinTarget_WhenScheduleExceedsTarget_ReturnsValidationError()
    {
        var target = new DateOnly(2026, 8, 20);
        var scheduleDate = new DateTime(2026, 8, 21, 10, 0, 0, DateTimeKind.Utc);

        var error = ProjectTimelineDateValidator.ValidateScheduleDateWithinTarget(scheduleDate, target);

        Assert.NotNull(error);
        Assert.Equal(ProjectScheduleErrorCodes.ScheduleDateExceedsTarget, error!.Code);
    }

    [Fact]
    public void ValidateScheduleDateWithinTarget_WhenScheduleOnTarget_ReturnsNull()
    {
        var target = new DateOnly(2026, 8, 20);
        var scheduleDate = new DateTime(2026, 8, 20, 15, 0, 0, DateTimeKind.Utc);

        var error = ProjectTimelineDateValidator.ValidateScheduleDateWithinTarget(scheduleDate, target);

        Assert.Null(error);
    }

    [Fact]
    public void ValidateScheduleDateWithinTarget_WhenTargetMissing_ReturnsNull()
    {
        var scheduleDate = DateTime.UtcNow.AddDays(30);

        var error = ProjectTimelineDateValidator.ValidateScheduleDateWithinTarget(scheduleDate, targetCompletionDate: null);

        Assert.Null(error);
    }
}
