#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.Repositories.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.Repositories;

public sealed class ProjectPhaseTimelineRepositoryTests
{
    [Fact]
    public async Task GetByProjectAsync_ReturnsTimelinesOrderedByDueDate()
    {
        await using var context = CreateContext();
        var projectId = Guid.NewGuid();
        var production = CreateTimeline(projectId, ProjectPhaseType.PRODUCTION, new DateOnly(2026, 9, 25));
        var proposal = CreateTimeline(projectId, ProjectPhaseType.PROPOSAL, new DateOnly(2026, 9, 10));
        context.ProjectPhaseTimelineSet.AddRange(production, proposal, CreateTimeline(Guid.NewGuid(), ProjectPhaseType.PROPOSAL, new DateOnly(2026, 9, 1)));
        await context.SaveChangesAsync();
        var repository = new ProjectPhaseTimelineRepository(context);

        var result = await repository.GetByProjectAsync(projectId);

        Assert.Equal([proposal.ProjectPhaseTimelineId, production.ProjectPhaseTimelineId], result.Select(item => item.ProjectPhaseTimelineId));
    }

    [Fact]
    public async Task GetByProjectAndPhaseAsync_ReturnsMatchingTimeline()
    {
        await using var context = CreateContext();
        var projectId = Guid.NewGuid();
        var timeline = CreateTimeline(projectId, ProjectPhaseType.PROPOSAL, new DateOnly(2026, 9, 10));
        context.ProjectPhaseTimelineSet.Add(timeline);
        await context.SaveChangesAsync();
        var repository = new ProjectPhaseTimelineRepository(context);

        var result = await repository.GetByProjectAndPhaseAsync(projectId, ProjectPhaseType.PROPOSAL);

        Assert.NotNull(result);
        Assert.Equal(timeline.ProjectPhaseTimelineId, result.ProjectPhaseTimelineId);
    }

    private static AppDbContext CreateContext()
    {
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
    }

    private static ProjectPhaseTimeline CreateTimeline(
        Guid projectId,
        ProjectPhaseType phase,
        DateOnly dueDate)
    {
        return new ProjectPhaseTimeline
        {
            ProjectPhaseTimelineId = Guid.NewGuid(),
            ProjectId = projectId,
            Phase = phase,
            DueDate = dueDate,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
