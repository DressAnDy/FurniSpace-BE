using System;
using FurniSpace.Application.Common.ProjectShowcases;
using Xunit;

namespace FurniSpace.Application.Tests.ProjectShowcases;

public sealed class PublicShowcaseProjectProjectionTests
{
    [Fact]
    public void ToImplementationDurationDays_ReturnsElapsedCalendarDays()
    {
        var submittedAt = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        var completedAt = new DateTime(2026, 6, 20, 18, 30, 0, DateTimeKind.Utc);

        var duration = PublicShowcaseProjectProjection.ToImplementationDurationDays(submittedAt, completedAt);

        Assert.Equal(19, duration);
    }

    [Fact]
    public void ToImplementationDurationDays_WhenSubmittedAtNull_ReturnsNull()
    {
        var duration = PublicShowcaseProjectProjection.ToImplementationDurationDays(
            null,
            new DateTime(2026, 6, 20, 0, 0, 0, DateTimeKind.Utc));

        Assert.Null(duration);
    }

    [Fact]
    public void ToImplementationDurationDays_WhenCompletedAtNull_ReturnsNull()
    {
        var duration = PublicShowcaseProjectProjection.ToImplementationDurationDays(
            new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            null);

        Assert.Null(duration);
    }

    [Fact]
    public void ToCompletedDate_UsesUtcDate()
    {
        var completedAt = new DateTime(2026, 8, 20, 23, 59, 0, DateTimeKind.Utc);

        var completedDate = PublicShowcaseProjectProjection.ToCompletedDate(completedAt);

        Assert.Equal(new DateOnly(2026, 8, 20), completedDate);
    }

    [Fact]
    public void ToCompletionYear_UsesUtcYear()
    {
        var completionYear = PublicShowcaseProjectProjection.ToCompletionYear(
            new DateTime(2026, 12, 31, 23, 59, 0, DateTimeKind.Utc));

        Assert.Equal(2026, completionYear);
    }
}
