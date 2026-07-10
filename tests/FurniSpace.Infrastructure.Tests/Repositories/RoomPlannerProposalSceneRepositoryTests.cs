#nullable enable

using System;
using System.Threading.Tasks;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.Repositories.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.Repositories;

public sealed class RoomPlannerProposalSceneRepositoryTests
{
    [Fact]
    public async Task GetContextAsync_WhenSceneExists_ReturnsProjectAndProposalContext()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new RoomPlannerProposalSceneRepository(context);

        var result = await repository.GetContextAsync(data.SceneId);

        Assert.NotNull(result);
        Assert.Equal(data.SceneId, result.SceneId);
        Assert.Equal(data.ProposalId, result.ProposalId);
        Assert.Equal(data.ProjectId, result.ProjectId);
        Assert.Equal(data.ProjectAreaId, result.ProjectAreaId);
        Assert.Equal("mongo-scene-id", result.MongoSceneId);
        Assert.Equal(ProposalStatus.DRAFT, result.ProposalStatus);
        Assert.Equal(data.CustomerId, result.CustomerId);
        Assert.Equal(data.SalesId, result.AssignedSalesId);
        Assert.Equal(data.DesignerId, result.AssignedDesignerId);
    }

    [Fact]
    public async Task GetContextAsync_WhenSceneMissing_ReturnsNull()
    {
        await using var context = CreateContext();
        var repository = new RoomPlannerProposalSceneRepository(context);

        var result = await repository.GetContextAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateMongoSceneIdAsync_WhenSceneExists_UpdatesMongoSceneIdAndTimestamp()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new RoomPlannerProposalSceneRepository(context);

        await repository.UpdateMongoSceneIdAsync(data.SceneId, "new-mongo-id");

        var scene = await context.ProposalSceneSet.SingleAsync(scene => scene.SceneId == data.SceneId);
        Assert.Equal("new-mongo-id", scene.MongoSceneId);
        Assert.NotNull(scene.UpdatedAt);
    }

    [Fact]
    public async Task UpdateMongoSceneIdAsync_WhenSceneMissing_DoesNothing()
    {
        await using var context = CreateContext();
        var repository = new RoomPlannerProposalSceneRepository(context);

        await repository.UpdateMongoSceneIdAsync(Guid.NewGuid(), "new-mongo-id");

        Assert.Empty(context.ProposalSceneSet);
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
        var proposalId = Guid.NewGuid();
        var sceneId = Guid.NewGuid();
        var projectAreaId = Guid.NewGuid();

        context.ProjectSet.Add(new Project
        {
            ProjectId = projectId,
            CustomerId = customerId,
            AssignedSalesId = salesId,
            AssignedDesignerId = designerId,
            ProjectName = "Cafe",
            BusinessType = "Cafe",
            FurnitureRequirement = "Tables",
            Status = ProjectStatus.PROPOSAL_CONSULTING
        });
        context.ProposalSet.Add(new Proposal
        {
            ProposalId = proposalId,
            ProjectId = projectId,
            ProposalName = "Proposal",
            Status = ProposalStatus.DRAFT,
            PublishedAt = DateTime.UtcNow
        });
        context.ProposalSceneSet.Add(new ProposalScene
        {
            SceneId = sceneId,
            ProposalId = proposalId,
            ProjectAreaId = projectAreaId,
            SceneName = "Scene",
            SceneType = ProposalSceneType.THREE_D,
            MongoSceneId = "mongo-scene-id"
        });
        await context.SaveChangesAsync();

        return new SeededData(customerId, salesId, designerId, projectId, proposalId, sceneId, projectAreaId);
    }

    private sealed record SeededData(
        Guid CustomerId,
        Guid SalesId,
        Guid DesignerId,
        Guid ProjectId,
        Guid ProposalId,
        Guid SceneId,
        Guid ProjectAreaId);
}
