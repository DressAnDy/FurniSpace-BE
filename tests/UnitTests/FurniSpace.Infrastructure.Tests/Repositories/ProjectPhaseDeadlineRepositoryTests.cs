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

public sealed class ProjectPhaseDeadlineRepositoryTests
{
    [Fact]
    public async Task GetByProjectAsync_ReturnsDeadlinesOrderedByDueDate()
    {
        await using var context = CreateContext();
        var projectId = Guid.NewGuid();
        var production = CreateDeadline(projectId, ProjectPhaseType.PRODUCTION, new DateOnly(2026, 9, 25));
        var proposal = CreateDeadline(projectId, ProjectPhaseType.PROPOSAL, new DateOnly(2026, 9, 10));
        context.ProjectPhaseDeadlineSet.AddRange(production, proposal, CreateDeadline(Guid.NewGuid(), ProjectPhaseType.PROPOSAL, new DateOnly(2026, 9, 1)));
        await context.SaveChangesAsync();
        var repository = new ProjectPhaseDeadlineRepository(context);

        var result = await repository.GetByProjectAsync(projectId);

        Assert.Equal([proposal.ProjectPhaseDeadlineId, production.ProjectPhaseDeadlineId], result.Select(item => item.ProjectPhaseDeadlineId));
    }

    [Fact]
    public async Task GetByProjectAndPhaseAsync_ReturnsMatchingDeadline()
    {
        await using var context = CreateContext();
        var projectId = Guid.NewGuid();
        var deadline = CreateDeadline(projectId, ProjectPhaseType.PROPOSAL, new DateOnly(2026, 9, 10));
        context.ProjectPhaseDeadlineSet.Add(deadline);
        await context.SaveChangesAsync();
        var repository = new ProjectPhaseDeadlineRepository(context);

        var result = await repository.GetByProjectAndPhaseAsync(projectId, ProjectPhaseType.PROPOSAL);

        Assert.NotNull(result);
        Assert.Equal(deadline.ProjectPhaseDeadlineId, result.ProjectPhaseDeadlineId);
    }

    private static AppDbContext CreateContext()
    {
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
    }

    private static ProjectPhaseDeadline CreateDeadline(
        Guid projectId,
        ProjectPhaseType phase,
        DateOnly dueDate)
    {
        return new ProjectPhaseDeadline
        {
            ProjectPhaseDeadlineId = Guid.NewGuid(),
            ProjectId = projectId,
            Phase = phase,
            DueDate = dueDate,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
