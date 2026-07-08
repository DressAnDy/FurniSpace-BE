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

public sealed class ProjectAreaRepositoryTests
{
    [Fact]
    public async Task GetDetailAsync_ReturnsJoinedProjectAssignmentData()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new ProjectAreaRepository(context);

        var detail = await repository.GetDetailAsync(data.ActiveAreaId);

        Assert.NotNull(detail);
        Assert.Equal(data.ProjectId, detail.ProjectId);
        Assert.Equal(data.CustomerId, detail.CustomerId);
        Assert.Equal(data.SalesId, detail.AssignedSalesId);
        Assert.Equal(data.DesignerId, detail.AssignedDesignerId);
        Assert.Equal("Active Area", detail.AreaName);
    }

    [Fact]
    public async Task GetListByProjectAsync_ExcludesCancelledAreasByDefault()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new ProjectAreaRepository(context);

        var items = await repository.GetListByProjectAsync(data.ProjectId, includeCancelled: false);

        Assert.Equal(3, items.Count);
        Assert.DoesNotContain(items, item => item.Status == ProjectAreaStatus.CANCELLED);
    }

    [Fact]
    public async Task GetListByProjectAsync_IncludesCancelledAreasWhenRequested()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new ProjectAreaRepository(context);

        var items = await repository.GetListByProjectAsync(data.ProjectId, includeCancelled: true);

        Assert.Equal(4, items.Count);
        Assert.Contains(items, item => item.AreaName == "Cancelled Area");
    }

    [Fact]
    public async Task BelongsToProjectAsync_ReturnsTrueForMatchingProject()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new ProjectAreaRepository(context);

        var belongs = await repository.BelongsToProjectAsync(data.ActiveAreaId, data.ProjectId);
        var otherProject = await repository.BelongsToProjectAsync(data.ActiveAreaId, Guid.NewGuid());

        Assert.True(belongs);
        Assert.False(otherProject);
    }

    [Fact]
    public async Task HasActiveUsageAsync_ReturnsTrueWhenActiveProposalSceneExists()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new ProjectAreaRepository(context);

        var inUse = await repository.HasActiveUsageAsync(data.ActiveAreaId);

        Assert.True(inUse);
    }

    [Fact]
    public async Task HasActiveUsageAsync_ReturnsTrueWhenProposalItemExists()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new ProjectAreaRepository(context);

        var inUse = await repository.HasActiveUsageAsync(data.ItemOnlyAreaId);

        Assert.True(inUse);
    }

    [Fact]
    public async Task HasActiveUsageAsync_ReturnsFalseWhenAreaIsUnused()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new ProjectAreaRepository(context);

        var inUse = await repository.HasActiveUsageAsync(data.UnusedAreaId);

        Assert.False(inUse);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<SeededData> SeedAsync(AppDbContext context)
    {
        var customerId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var activeAreaId = Guid.NewGuid();
        var cancelledAreaId = Guid.NewGuid();
        var unusedAreaId = Guid.NewGuid();
        var itemOnlyAreaId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var sceneId = Guid.NewGuid();

        context.ProjectSet.Add(new Project
        {
            ProjectId = projectId,
            CustomerId = customerId,
            AssignedSalesId = salesId,
            AssignedDesignerId = designerId,
            ProjectName = "Luxury Cafe",
            BusinessType = "Cafe",
            FurnitureRequirement = "Tables",
            Status = ProjectStatus.PROPOSAL_DRAFTING
        });
        context.ProjectAreaSet.AddRange(
            new ProjectArea
            {
                ProjectAreaId = activeAreaId,
                ProjectId = projectId,
                AreaName = "Active Area",
                Status = ProjectAreaStatus.VERIFIED,
                CreatedAt = DateTime.UtcNow
            },
            new ProjectArea
            {
                ProjectAreaId = cancelledAreaId,
                ProjectId = projectId,
                AreaName = "Cancelled Area",
                Status = ProjectAreaStatus.CANCELLED,
                CreatedAt = DateTime.UtcNow
            },
            new ProjectArea
            {
                ProjectAreaId = unusedAreaId,
                ProjectId = projectId,
                AreaName = "Unused Area",
                Status = ProjectAreaStatus.DRAFT,
                CreatedAt = DateTime.UtcNow
            },
            new ProjectArea
            {
                ProjectAreaId = itemOnlyAreaId,
                ProjectId = projectId,
                AreaName = "Item Area",
                Status = ProjectAreaStatus.DRAFT,
                CreatedAt = DateTime.UtcNow
            });
        context.ProposalSet.Add(new Proposal
        {
            ProposalId = proposalId,
            ProjectId = projectId,
            ProposalName = "Published proposal",
            Status = ProposalStatus.PUBLISHED,
            VersionNo = 1,
            CreatedAt = DateTime.UtcNow
        });
        context.ProposalSceneSet.Add(new ProposalScene
        {
            SceneId = sceneId,
            ProposalId = proposalId,
            ProjectAreaId = activeAreaId,
            SceneName = "Main layout",
            SceneType = ProposalSceneType.THREE_D,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        context.ProposalItemSet.Add(new ProposalItem
        {
            ProposalItemId = Guid.NewGuid(),
            ProposalId = proposalId,
            ProjectAreaId = itemOnlyAreaId,
            ItemName = "Chair",
            Quantity = 1,
            CreatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        return new SeededData(
            projectId,
            customerId,
            salesId,
            designerId,
            activeAreaId,
            unusedAreaId,
            itemOnlyAreaId);
    }

    private sealed record SeededData(
        Guid ProjectId,
        Guid CustomerId,
        Guid SalesId,
        Guid DesignerId,
        Guid ActiveAreaId,
        Guid UnusedAreaId,
        Guid ItemOnlyAreaId);
}
