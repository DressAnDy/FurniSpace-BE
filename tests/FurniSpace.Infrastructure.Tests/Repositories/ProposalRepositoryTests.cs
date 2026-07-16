#nullable enable

using System;
using System.Threading.Tasks;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.ReadModels.Proposals;
using FurniSpace.Infrastructure.Repositories.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.Repositories;

public sealed class ProposalRepositoryTests
{
    [Fact]
    public async Task GetProjectAccessAndContextAsync_ReturnProjectAssignmentData()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new ProposalRepository(context);

        var access = await repository.GetProjectAccessAsync(data.ProjectId);
        var proposalContext = await repository.GetProposalContextAsync(data.PublishedProposalId);

        Assert.NotNull(access);
        Assert.Equal(data.CustomerId, access.CustomerId);
        Assert.Equal(data.SalesId, access.AssignedSalesId);
        Assert.Equal(data.DesignerId, access.AssignedDesignerId);
        Assert.Equal(ProjectStatus.PROPOSAL_CONSULTING, access.ProjectStatus);
        Assert.NotNull(proposalContext);
        Assert.Equal(data.ProjectId, proposalContext.ProjectId);
        Assert.Equal(ProposalStatus.PUBLISHED, proposalContext.ProposalStatus);
    }

    [Fact]
    public async Task GetListAsync_WithCustomerVisibleOnly_ReturnsOnlyVisibleStatuses()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new ProposalRepository(context);

        var proposals = await repository.GetListAsync(new ProposalListQueryReadModel
        {
            ProjectId = data.ProjectId,
            CustomerVisibleOnly = true,
            Page = 1,
            Limit = 10
        });
        var count = await repository.CountListAsync(new ProposalListQueryReadModel
        {
            ProjectId = data.ProjectId,
            CustomerVisibleOnly = true,
            Page = 1,
            Limit = 10
        });

        Assert.Equal(2, count);
        Assert.Equal(2, proposals.Count);
        Assert.All(proposals, proposal => Assert.NotEqual(ProposalStatus.DRAFT, proposal.Status));
        Assert.Equal(3, proposals[0].VersionNo);
        Assert.Equal(2, proposals[1].VersionNo);
    }

    [Fact]
    public async Task GetListAsync_WithCustomerVisibleOnly_IncludesRejectedProposals()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var rejectedProposalId = Guid.NewGuid();
        context.ProposalSet.Add(CreateProposal(
            rejectedProposalId,
            data.ProjectId,
            "Rejected proposal",
            ProposalStatus.REJECTED,
            versionNo: 4));
        await context.SaveChangesAsync();

        var repository = new ProposalRepository(context);
        var proposals = await repository.GetListAsync(new ProposalListQueryReadModel
        {
            ProjectId = data.ProjectId,
            CustomerVisibleOnly = true,
            Page = 1,
            Limit = 10
        });

        Assert.Equal(3, proposals.Count);
        Assert.Contains(proposals, proposal => proposal.ProposalId == rejectedProposalId);
        Assert.Contains(proposals, proposal => proposal.Status == ProposalStatus.REJECTED);
    }

    [Fact]
    public async Task GetListAsync_WithStatusFilter_ReturnsMatchingProposals()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new ProposalRepository(context);

        var proposals = await repository.GetListAsync(new ProposalListQueryReadModel
        {
            ProjectId = data.ProjectId,
            Status = ProposalStatus.DRAFT,
            Page = 1,
            Limit = 10
        });
        var count = await repository.CountByProjectAsync(data.ProjectId);

        Assert.Equal(3, count);
        Assert.Single(proposals);
        Assert.Equal(data.DraftProposalId, proposals[0].ProposalId);
    }

    [Fact]
    public async Task GetDetailAsync_ReturnsActiveScenesWithPreviewAndItems()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new ProposalRepository(context);

        var detail = await repository.GetDetailAsync(data.PublishedProposalId);

        Assert.NotNull(detail);
        Assert.Equal(data.ProjectId, detail.ProjectId);
        Assert.Single(detail.Scenes);
        Assert.Equal(data.ActiveSceneId, detail.Scenes[0].SceneId);
        Assert.Equal("https://cdn.furnispace.test/preview.png", detail.Scenes[0].PreviewFileUrl);
        Assert.Single(detail.Items);
        Assert.Equal("Cafe Chair", detail.Items[0].ProductNameSnapshot);
        Assert.Equal(4800000m, detail.Items[0].SubtotalAmount);
    }

    [Fact]
    public async Task GetLatestPublishedByProjectAsync_ReturnsVisibleProposalWithScenesAndItems()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new ProposalRepository(context);

        var detail = await repository.GetLatestPublishedByProjectAsync(data.ProjectId);

        Assert.NotNull(detail);
        Assert.Equal(data.PublishedProposalId, detail.ProposalId);
        Assert.Equal(data.CustomerId, detail.CustomerId);
        Assert.Equal(data.SalesId, detail.AssignedSalesId);
        Assert.Equal(data.DesignerId, detail.AssignedDesignerId);
        Assert.Single(detail.Scenes);
        Assert.Single(detail.Items);
    }

    [Fact]
    public async Task GetLatestPublishedByProjectAsync_WithMissingProject_ReturnsNull()
    {
        await using var context = CreateContext();
        await SeedAsync(context);
        var repository = new ProposalRepository(context);

        var detail = await repository.GetLatestPublishedByProjectAsync(Guid.NewGuid());

        Assert.Null(detail);
    }

    [Fact]
    public async Task GetScenesAsync_WithFiltersAndPagination_ReturnsMatchingScenes()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new ProposalRepository(context);
        var query = new ProposalSceneListQueryReadModel
        {
            ProposalId = data.PublishedProposalId,
            SceneType = ProposalSceneType.TWO_D,
            IsActive = false,
            Page = 1,
            Limit = 1
        };

        var scenes = await repository.GetScenesAsync(query);
        var count = await repository.CountScenesAsync(query);

        Assert.Equal(1, count);
        var scene = Assert.Single(scenes);
        Assert.Equal("Inactive layout", scene.SceneName);
        Assert.Equal(ProposalSceneType.TWO_D, scene.SceneType);
        Assert.False(scene.IsActive);
    }

    [Fact]
    public async Task GetScenesAsync_WithActiveOnly_IgnoresInactiveFilter()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new ProposalRepository(context);
        var query = new ProposalSceneListQueryReadModel
        {
            ProposalId = data.PublishedProposalId,
            IsActive = false,
            ActiveOnly = true,
            Page = 1,
            Limit = 10
        };

        var scenes = await repository.GetScenesAsync(query);
        var count = await repository.CountScenesAsync(query);

        Assert.Equal(1, count);
        var scene = Assert.Single(scenes);
        Assert.Equal(data.ActiveSceneId, scene.SceneId);
        Assert.True(scene.IsActive);
        Assert.Equal("https://cdn.furnispace.test/preview.png", scene.PreviewFileUrl);
    }

    [Fact]
    public async Task GetSceneDetailAsync_ReturnsSceneMetadataAndProjectAccessData()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new ProposalRepository(context);

        var scene = await repository.GetSceneDetailAsync(data.ActiveSceneId);

        Assert.NotNull(scene);
        Assert.Equal(data.ActiveSceneId, scene.SceneId);
        Assert.Equal(data.ProjectId, scene.ProjectId);
        Assert.Equal(data.CustomerId, scene.CustomerId);
        Assert.Equal(data.SalesId, scene.AssignedSalesId);
        Assert.Equal(data.DesignerId, scene.AssignedDesignerId);
        Assert.Equal(ProposalStatus.PUBLISHED, scene.ProposalStatus);
        Assert.Equal("mongo-scene-id", scene.MongoSceneId);
        Assert.Equal("https://cdn.furnispace.test/preview.png", scene.PreviewFileUrl);
    }

    [Fact]
    public async Task GetSceneDetailAsync_WithMissingScene_ReturnsNull()
    {
        await using var context = CreateContext();
        await SeedAsync(context);
        var repository = new ProposalRepository(context);

        var scene = await repository.GetSceneDetailAsync(Guid.NewGuid());

        Assert.Null(scene);
    }

    [Fact]
    public async Task GetItemsAsync_WithSceneFilter_ReturnsPagedItemsWithVersionSnapshot()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new ProposalRepository(context);
        var query = new ProposalItemListQueryReadModel
        {
            ProposalId = data.PublishedProposalId,
            SceneId = data.ActiveSceneId,
            Page = 1,
            Limit = 10
        };

        var items = await repository.GetItemsAsync(query);
        var count = await repository.CountItemsAsync(query);

        Assert.Equal(1, count);
        var item = Assert.Single(items);
        Assert.Equal(data.ProposalItemId, item.ProposalItemId);
        Assert.Equal(data.PublishedProposalId, item.ProposalId);
        Assert.Equal(data.ActiveSceneId, item.SceneId);
        Assert.Equal("Cafe Chair", item.ProductNameSnapshot);
        Assert.Equal("Brown Wood", item.VersionNameSnapshot);
        Assert.Equal("cm", item.DimensionUnit);
        Assert.Equal("Wood", item.MaterialSnapshot);
        Assert.Equal("Brown", item.ColorSnapshot);
        Assert.Equal(4800000m, item.SubtotalAmount);
    }

    [Fact]
    public async Task GetItemDetailAsync_ReturnsProjectAssignmentData()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new ProposalRepository(context);

        var item = await repository.GetItemDetailAsync(data.ProposalItemId);

        Assert.NotNull(item);
        Assert.Equal(data.ProposalItemId, item.ProposalItemId);
        Assert.Equal(data.ProjectId, item.ProjectId);
        Assert.Equal(data.CustomerId, item.CustomerId);
        Assert.Equal(data.SalesId, item.AssignedSalesId);
        Assert.Equal(data.DesignerId, item.AssignedDesignerId);
        Assert.Equal(ProposalStatus.PUBLISHED, item.ProposalStatus);
        Assert.Equal("Brown Wood", item.VersionNameSnapshot);
    }

    [Fact]
    public async Task GetItemEntityAndRemoveItem_UpdateProposalItems()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new ProposalRepository(context);

        var item = await repository.GetItemEntityAsync(data.ProposalItemId);
        Assert.NotNull(item);

        repository.RemoveItem(item);
        await context.SaveChangesAsync();

        Assert.DoesNotContain(context.ProposalItemSet, entity => entity.ProposalItemId == data.ProposalItemId);
    }

    [Fact]
    public async Task AddSceneAsync_AddsSceneAndCountScenesIncludesIt()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new ProposalRepository(context);
        var scene = new ProposalScene
        {
            SceneId = Guid.NewGuid(),
            ProposalId = data.DraftProposalId,
            SceneName = "New scene",
            SceneType = ProposalSceneType.TWO_D,
            IsActive = true,
            VersionNo = 1
        };

        await repository.AddSceneAsync(scene);
        await context.SaveChangesAsync();
        var count = await repository.CountScenesAsync(data.DraftProposalId);

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task GetSceneContextAndItemsAsync_ReturnSceneAndExistingItems()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new ProposalRepository(context);

        var scene = await repository.GetSceneContextAsync(data.PublishedProposalId, data.ActiveSceneId);
        var sceneById = await repository.GetSceneContextBySceneIdAsync(data.ActiveSceneId);
        var items = await repository.GetItemsBySceneAsync(data.PublishedProposalId, data.ActiveSceneId);

        Assert.NotNull(scene);
        Assert.Equal(data.ProjectId, scene.ProjectId);
        Assert.Equal(data.SalesId, scene.AssignedSalesId);
        Assert.NotNull(sceneById);
        Assert.Equal(data.PublishedProposalId, sceneById.ProposalId);
        Assert.Single(items);
        Assert.Equal("Cafe Chair", items[0].ItemName);
    }

    [Fact]
    public async Task SceneMetadataHelpers_ReturnExpectedValues()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new ProposalRepository(context);

        var scene = await repository.GetSceneEntityAsync(data.ActiveSceneId);
        var hasActiveScene = await repository.HasActiveSceneAsync(data.PublishedProposalId);
        var fileExists = await repository.FileExistsAsync(data.PreviewFileId);
        var areaBelongsToProject = await repository.ProjectAreaBelongsToProjectAsync(data.ProjectAreaId, data.ProjectId);
        var areaBelongsToOtherProject = await repository.ProjectAreaBelongsToProjectAsync(data.ProjectAreaId, Guid.NewGuid());

        Assert.NotNull(scene);
        Assert.Equal(data.ActiveSceneId, scene.SceneId);
        Assert.True(hasActiveScene);
        Assert.True(fileExists);
        Assert.True(areaBelongsToProject);
        Assert.False(areaBelongsToOtherProject);
    }

    [Fact]
    public async Task AddItemAsync_AddsProposalItem()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new ProposalRepository(context);
        var item = new ProposalItem
        {
            ProposalItemId = Guid.NewGuid(),
            ProposalId = data.DraftProposalId,
            SceneId = data.ActiveSceneId,
            ItemName = "Cafe Table",
            Quantity = 2
        };

        await repository.AddItemAsync(item);
        await context.SaveChangesAsync();

        Assert.Contains(context.ProposalItemSet, entity => entity.ProposalItemId == item.ProposalItemId);
    }

    [Fact]
    public async Task GetProposalEntityAndRejectOtherActiveProposalsAsync_UpdateProposalStatuses()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new ProposalRepository(context);

        var proposal = await repository.GetProposalEntityAsync(data.PublishedProposalId);
        await repository.RejectOtherActiveProposalsAsync(
            data.ProjectId,
            data.PublishedProposalId,
            DateTime.UtcNow);
        await context.SaveChangesAsync();

        Assert.NotNull(proposal);
        Assert.Equal(ProposalStatus.PUBLISHED, proposal.Status);
        var draft = await context.ProposalSet.SingleAsync(item => item.ProposalId == data.DraftProposalId);
        var selected = await context.ProposalSet.SingleAsync(item => item.ProposalId == data.SelectedProposalId);
        Assert.Equal(ProposalStatus.REJECTED, draft.Status);
        Assert.Equal(ProposalStatus.REJECTED, selected.Status);
        Assert.NotNull(draft.RejectedAt);
    }

    [Fact]
    public async Task GetDetailAsync_WithMissingProposal_ReturnsNull()
    {
        await using var context = CreateContext();
        var repository = new ProposalRepository(context);

        var detail = await repository.GetDetailAsync(Guid.NewGuid());

        Assert.Null(detail);
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
        var draftProposalId = Guid.NewGuid();
        var publishedProposalId = Guid.NewGuid();
        var selectedProposalId = Guid.NewGuid();
        var activeSceneId = Guid.NewGuid();
        var inactiveSceneId = Guid.NewGuid();
        var previewFileId = Guid.NewGuid();
        var projectAreaId = Guid.NewGuid();
        var productVersionId = Guid.NewGuid();
        var proposalItemId = Guid.NewGuid();

        context.ProjectSet.Add(new Project
        {
            ProjectId = projectId,
            CustomerId = customerId,
            AssignedSalesId = salesId,
            AssignedDesignerId = designerId,
            ProjectName = "Luxury Cafe",
            BusinessType = "Cafe",
            FurnitureRequirement = "Tables",
            Status = ProjectStatus.PROPOSAL_CONSULTING
        });
        context.StoredFileSet.Add(new StoredFile
        {
            FileId = previewFileId,
            UploadedBy = designerId,
            OriginalFileName = "preview.png",
            StoredFileName = "preview.png",
            FileUrl = "https://cdn.furnispace.test/preview.png",
            StoragePath = "proposal/preview.png",
            MimeType = "image/png",
            FileSizeBytes = 10,
            Status = FileStatus.ACTIVE,
            UploadedAt = DateTime.UtcNow
        });
        context.ProjectAreaSet.Add(new ProjectArea
        {
            ProjectAreaId = projectAreaId,
            ProjectId = projectId,
            AreaName = "Main cafe area",
            Status = ProjectAreaStatus.VERIFIED,
            CreatedAt = DateTime.UtcNow
        });
        context.ProductVersionSet.Add(new ProductVersion
        {
            ProductVersionId = productVersionId,
            ProductId = Guid.NewGuid(),
            VersionCode = "PV-CHAIR-001",
            VersionName = "Brown Wood",
            Material = "Wood",
            Color = "Brown",
            DimensionUnit = "cm",
            EstimatedPrice = 1200000m
        });
        context.ProposalSet.AddRange(
            CreateProposal(draftProposalId, projectId, "Draft proposal", ProposalStatus.DRAFT, versionNo: 1),
            CreateProposal(publishedProposalId, projectId, "Published proposal", ProposalStatus.PUBLISHED, versionNo: 2),
            CreateProposal(selectedProposalId, projectId, "Selected proposal", ProposalStatus.SELECTED, versionNo: 3));
        context.ProposalSceneSet.AddRange(
            new ProposalScene
            {
                SceneId = activeSceneId,
                ProposalId = publishedProposalId,
                SceneName = "Main layout",
                SceneType = ProposalSceneType.THREE_D,
                MongoSceneId = "mongo-scene-id",
                ProjectAreaId = projectAreaId,
                PreviewFileId = previewFileId,
                VersionNo = 1,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new ProposalScene
            {
                SceneId = inactiveSceneId,
                ProposalId = publishedProposalId,
                SceneName = "Inactive layout",
                SceneType = ProposalSceneType.TWO_D,
                VersionNo = 2,
                IsActive = false,
                CreatedAt = DateTime.UtcNow
            });
        context.ProposalItemSet.Add(new ProposalItem
        {
            ProposalItemId = proposalItemId,
            ProposalId = publishedProposalId,
            SceneId = activeSceneId,
            ProductVersionId = productVersionId,
            ItemName = "Cafe Chair",
            Quantity = 4,
            Material = "Wood",
            Color = "Brown",
            Width = 45,
            Height = 80,
            Depth = 45,
            UnitPriceSnapshot = 1200000m,
            TotalPriceSnapshot = 4800000m,
            Note = "Use brown wood version.",
            UpdatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
        return new SeededData(
            customerId,
            salesId,
            designerId,
            projectId,
            draftProposalId,
            publishedProposalId,
            selectedProposalId,
            activeSceneId,
            previewFileId,
            projectAreaId,
            proposalItemId);
    }

    private static Proposal CreateProposal(
        Guid proposalId,
        Guid projectId,
        string proposalName,
        ProposalStatus status,
        int versionNo)
    {
        return new Proposal
        {
            ProposalId = proposalId,
            ProjectId = projectId,
            ProposalName = proposalName,
            Description = "Proposal description",
            Status = status,
            VersionNo = versionNo,
            CreatedAt = DateTime.UtcNow.AddMinutes(versionNo),
            UpdatedAt = DateTime.UtcNow.AddMinutes(versionNo)
        };
    }

    private sealed record SeededData(
        Guid CustomerId,
        Guid SalesId,
        Guid DesignerId,
        Guid ProjectId,
        Guid DraftProposalId,
        Guid PublishedProposalId,
        Guid SelectedProposalId,
        Guid ActiveSceneId,
        Guid PreviewFileId,
        Guid ProjectAreaId,
        Guid ProposalItemId);
}
