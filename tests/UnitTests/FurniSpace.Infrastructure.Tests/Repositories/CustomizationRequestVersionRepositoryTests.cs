#nullable enable

using System;
using System.Threading.Tasks;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.ReadModels.CustomizationRequests;
using FurniSpace.Infrastructure.Repositories.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.Repositories;

public sealed class CustomizationRequestVersionRepositoryTests
{
    [Fact]
    public async Task GetNextVersionNoAsync_WhenNoVersions_ReturnsOne()
    {
        await using var context = CreateContext();
        var requestId = Guid.NewGuid();
        var repository = new CustomizationRequestVersionRepository(context);

        var next = await repository.GetNextVersionNoAsync(requestId);

        Assert.Equal(1, next);
    }

    [Fact]
    public async Task GetNextVersionNoAsync_WhenVersionsExist_ReturnsMaxPlusOne()
    {
        await using var context = CreateContext();
        var graph = await SeedGraphAsync(context);
        var versionProduct = CreateVersionProduct(graph);
        context.ProductVersionSet.Add(versionProduct);
        context.CustomizationRequestVersionSet.Add(new CustomizationRequestVersion
        {
            CustomizationRequestVersionId = Guid.NewGuid(),
            CustomizationRequestId = graph.Request.CustomizationRequestId,
            ProductVersionId = versionProduct.ProductVersionId,
            VersionNo = 2,
            CreatedByDesignerId = graph.Project.AssignedDesignerId ?? Guid.NewGuid(),
            Status = CustomizationVersionStatus.DRAFT,
            FeasibilityStatus = ProductionFeasibilityStatus.PENDING,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        var repository = new CustomizationRequestVersionRepository(context);

        var next = await repository.GetNextVersionNoAsync(graph.Request.CustomizationRequestId);

        Assert.Equal(3, next);
    }

    [Fact]
    public async Task GetByRequestIdAsync_ReturnsVersionsOrderedByVersionNoWithProductVersion()
    {
        await using var context = CreateContext();
        var graph = await SeedGraphAsync(context);
        var firstProduct = CreateVersionProduct(graph, "Custom V1");
        var secondProduct = CreateVersionProduct(graph, "Custom V2");
        var firstVersion = CreateVersion(graph, firstProduct, versionNo: 1);
        var secondVersion = CreateVersion(graph, secondProduct, versionNo: 2);
        context.ProductVersionSet.AddRange(firstProduct, secondProduct);
        context.CustomizationRequestVersionSet.AddRange(firstVersion, secondVersion);
        await context.SaveChangesAsync();
        var repository = new CustomizationRequestVersionRepository(context);

        var items = await repository.GetByRequestIdAsync(graph.Request.CustomizationRequestId);

        Assert.Equal(2, items.Count);
        Assert.Equal(1, items[0].VersionNo);
        Assert.Equal(2, items[1].VersionNo);
        Assert.Equal("Custom V1", items[0].ProductVersion.VersionName);
        Assert.Equal("Custom V2", items[1].ProductVersion.VersionName);
    }

    [Fact]
    public async Task GetByIdForUpdateAsync_ReturnsTrackedVersion()
    {
        await using var context = CreateContext();
        var graph = await SeedGraphAsync(context);
        var versionProduct = CreateVersionProduct(graph);
        var version = CreateVersion(graph, versionProduct);
        context.ProductVersionSet.Add(versionProduct);
        context.CustomizationRequestVersionSet.Add(version);
        await context.SaveChangesAsync();
        var repository = new CustomizationRequestVersionRepository(context);

        var loaded = await repository.GetByIdForUpdateAsync(version.CustomizationRequestVersionId);

        Assert.NotNull(loaded);
        Assert.Equal(version.CustomizationRequestVersionId, loaded!.CustomizationRequestVersionId);
    }

    [Fact]
    public async Task GetByIdWithRequestAsync_IncludesCustomizationRequestAndProductVersion()
    {
        await using var context = CreateContext();
        var graph = await SeedGraphAsync(context);
        var versionProduct = CreateVersionProduct(graph);
        var version = CreateVersion(graph, versionProduct);
        context.ProductVersionSet.Add(versionProduct);
        context.CustomizationRequestVersionSet.Add(version);
        await context.SaveChangesAsync();
        var repository = new CustomizationRequestVersionRepository(context);

        var loaded = await repository.GetByIdWithRequestAsync(version.CustomizationRequestVersionId);

        Assert.NotNull(loaded);
        Assert.NotNull(loaded!.CustomizationRequest);
        Assert.NotNull(loaded.ProductVersion);
        Assert.Equal(graph.Request.CustomizationRequestId, loaded.CustomizationRequest!.CustomizationRequestId);
        Assert.Equal(versionProduct.ProductVersionId, loaded.ProductVersion!.ProductVersionId);
    }

    [Fact]
    public async Task GetProductionDetailAsync_ReturnsQueueDetailWhenVersionExists()
    {
        await using var context = CreateContext();
        var graph = await SeedGraphAsync(context);
        var versionProduct = CreateVersionProduct(graph);
        var version = CreateVersion(
            graph,
            versionProduct,
            status: CustomizationVersionStatus.REVIEWING,
            submittedForReviewAt: DateTime.UtcNow);
        context.ProductVersionSet.Add(versionProduct);
        context.CustomizationRequestVersionSet.Add(version);
        await context.SaveChangesAsync();
        var repository = new CustomizationRequestVersionRepository(context);

        var detail = await repository.GetProductionDetailAsync(version.CustomizationRequestVersionId);

        Assert.NotNull(detail);
        Assert.Equal(version.CustomizationRequestVersionId, detail!.Version.CustomizationRequestVersionId);
        Assert.Equal("Cafe Proposal", detail.ProposalName);
        Assert.Equal("Dining Chair", detail.SourceProductVersion.VersionName);
        Assert.Equal(graph.Project.ProjectName, detail.Request.ProjectName);
    }

    [Fact]
    public async Task GetProductionDetailAsync_WhenMissing_ReturnsNull()
    {
        await using var context = CreateContext();
        var repository = new CustomizationRequestVersionRepository(context);

        var detail = await repository.GetProductionDetailAsync(Guid.NewGuid());

        Assert.Null(detail);
    }

    [Fact]
    public async Task GetProductionQueueAsync_FiltersBySubmittedForReviewDateRange()
    {
        await using var context = CreateContext();
        var graph = await SeedGraphAsync(context);
        var inRangeProduct = CreateVersionProduct(graph);
        var outOfRangeProduct = CreateVersionProduct(graph);
        var inRange = CreateVersion(
            graph,
            inRangeProduct,
            status: CustomizationVersionStatus.REVIEWING,
            submittedForReviewAt: new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc));
        var outOfRange = CreateVersion(
            graph,
            outOfRangeProduct,
            status: CustomizationVersionStatus.REVIEWING,
            submittedForReviewAt: new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc));
        context.ProductVersionSet.AddRange(inRangeProduct, outOfRangeProduct);
        context.CustomizationRequestVersionSet.AddRange(inRange, outOfRange);
        await context.SaveChangesAsync();
        var repository = new CustomizationRequestVersionRepository(context);

        var items = await repository.GetProductionQueueAsync(new ProductionCustomizationVersionQueueQueryReadModel
        {
            Statuses = [CustomizationVersionStatus.REVIEWING],
            FeasibilityStatuses = [ProductionFeasibilityStatus.PENDING],
            FromDate = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc),
            ToDate = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc),
            Page = 1,
            PageSize = 10
        });

        Assert.Single(items);
        Assert.Equal(inRange.CustomizationRequestVersionId, items[0].Version.CustomizationRequestVersionId);
    }

    private static CustomizationRequestVersion CreateVersion(
        RequestGraphSeed graph,
        ProductVersion versionProduct,
        int versionNo = 1,
        CustomizationVersionStatus status = CustomizationVersionStatus.DRAFT,
        DateTime? submittedForReviewAt = null)
    {
        return new CustomizationRequestVersion
        {
            CustomizationRequestVersionId = Guid.NewGuid(),
            CustomizationRequestId = graph.Request.CustomizationRequestId,
            ProductVersionId = versionProduct.ProductVersionId,
            VersionNo = versionNo,
            CreatedByDesignerId = graph.Project.AssignedDesignerId ?? Guid.NewGuid(),
            VersionTitle = "Option A",
            Status = status,
            FeasibilityStatus = ProductionFeasibilityStatus.PENDING,
            SubmittedForReviewAt = submittedForReviewAt,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static ProductVersion CreateVersionProduct(RequestGraphSeed graph, string versionName = "Custom Chair")
    {
        return new ProductVersion
        {
            ProductVersionId = Guid.NewGuid(),
            ProductId = graph.SourceVersion.ProductId,
            ProjectId = graph.Project.ProjectId,
            VersionCode = $"PV-{Guid.NewGuid():N}"[..12],
            VersionName = versionName,
            VersionType = ProductVersionType.PROJECT_SPECIFIC,
            Status = ProductStatus.ACTIVE,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static async Task<RequestGraphSeed> SeedGraphAsync(AppDbContext context)
    {
        var project = new Project
        {
            ProjectId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            AssignedDesignerId = Guid.NewGuid(),
            ProjectName = "Cafe Project",
            ProjectCode = "PRJ-000001",
            Status = ProjectStatus.PROPOSAL_CONSULTING
        };
        var proposal = new Proposal
        {
            ProposalId = Guid.NewGuid(),
            ProjectId = project.ProjectId,
            ProposalName = "Cafe Proposal",
            Status = ProposalStatus.PUBLISHED
        };
        var sourceVersion = new ProductVersion
        {
            ProductVersionId = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            VersionCode = "PV-SRC-001",
            VersionName = "Dining Chair",
            Status = ProductStatus.ACTIVE
        };
        var request = new CustomizationRequest
        {
            CustomizationRequestId = Guid.NewGuid(),
            ProjectId = project.ProjectId,
            ProposalId = proposal.ProposalId,
            SourceProductVersionId = sourceVersion.ProductVersionId,
            RequestTitle = "Change material",
            Status = CustomizationStatus.REVIEWING,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.ProjectSet.Add(project);
        context.ProposalSet.Add(proposal);
        context.ProductVersionSet.Add(sourceVersion);
        context.CustomizationRequestSet.Add(request);
        await context.SaveChangesAsync();
        return new RequestGraphSeed(project, proposal, sourceVersion, request);
    }

    private sealed record RequestGraphSeed(
        Project Project,
        Proposal Proposal,
        ProductVersion SourceVersion,
        CustomizationRequest Request);

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }
}
