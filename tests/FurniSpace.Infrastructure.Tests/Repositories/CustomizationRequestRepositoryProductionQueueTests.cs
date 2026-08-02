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

public sealed class CustomizationRequestRepositoryProductionQueueTests
{
    [Fact]
    public async Task GetProductionQueueAsync_FiltersByStatusAndOrdersBySubmittedForReviewAtDescending()
    {
        await using var context = CreateContext();
        var data = await SeedQueueGraphAsync(context);
        var older = CreateQueueVersion(data, DateTime.UtcNow.AddHours(-2));
        var newer = CreateQueueVersion(data, DateTime.UtcNow.AddHours(-1));
        var accepted = CreateQueueVersion(data, DateTime.UtcNow, CustomizationVersionStatus.ACCEPTED);
        context.ProductVersionSet.AddRange(older.ProductVersion!, newer.ProductVersion!, accepted.ProductVersion!);
        context.CustomizationRequestVersionSet.AddRange(older, newer, accepted);
        await context.SaveChangesAsync();
        var repository = new CustomizationRequestVersionRepository(context);

        var items = await repository.GetProductionQueueAsync(new ProductionCustomizationVersionQueueQueryReadModel
        {
            Statuses = [CustomizationVersionStatus.REVIEWING],
            FeasibilityStatuses = [ProductionFeasibilityStatus.PENDING],
            Page = 1,
            PageSize = 10
        });

        Assert.Equal(2, items.Count);
        Assert.Contains(items, item => item.Version.CustomizationRequestVersionId == newer.CustomizationRequestVersionId);
        Assert.Contains(items, item => item.Version.CustomizationRequestVersionId == older.CustomizationRequestVersionId);
        Assert.True(items[0].Version.SubmittedForReviewAt >= items[1].Version.SubmittedForReviewAt);
        Assert.Equal("Cafe Proposal", items[0].ProposalName);
        Assert.Equal("Dining Chair", items[0].SourceProductVersion.VersionName);
        Assert.Equal(data.Project.ProjectName, items[0].Request.ProjectName);
    }

    [Fact]
    public async Task CountProductionQueueAsync_AppliesProjectProposalAndMaterialFilters()
    {
        await using var context = CreateContext();
        var data = await SeedQueueGraphAsync(context);
        var matching = CreateQueueVersion(data, DateTime.UtcNow.AddMinutes(-5), materialAvailable: true);
        context.ProductVersionSet.Add(matching.ProductVersion!);
        context.CustomizationRequestVersionSet.Add(matching);
        await context.SaveChangesAsync();
        var repository = new CustomizationRequestVersionRepository(context);

        var count = await repository.CountProductionQueueAsync(new ProductionCustomizationVersionQueueQueryReadModel
        {
            Statuses = [CustomizationVersionStatus.REVIEWING],
            FeasibilityStatuses = [ProductionFeasibilityStatus.PENDING],
            ProjectId = data.Project.ProjectId,
            ProposalId = data.Proposal.ProposalId,
            MaterialAvailable = true,
            Page = 1,
            PageSize = 10
        });

        Assert.Equal(1, count);
    }

    private static CustomizationRequestVersion CreateQueueVersion(
        QueueGraphSeed data,
        DateTime submittedForReviewAt,
        CustomizationVersionStatus status = CustomizationVersionStatus.REVIEWING,
        bool? materialAvailable = null)
    {
        var versionProduct = new ProductVersion
        {
            ProductVersionId = Guid.NewGuid(),
            ProductId = data.ProductVersion.ProductId,
            ProjectId = data.Project.ProjectId,
            VersionCode = $"PV-{Guid.NewGuid():N}"[..12],
            VersionName = "Custom Chair",
            VersionType = ProductVersionType.PROJECT_SPECIFIC,
            Status = ProductStatus.ACTIVE,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        return new CustomizationRequestVersion
        {
            CustomizationRequestVersionId = Guid.NewGuid(),
            CustomizationRequestId = data.Request.CustomizationRequestId,
            ProductVersionId = versionProduct.ProductVersionId,
            VersionNo = 1,
            CreatedByDesignerId = data.Project.AssignedDesignerId ?? Guid.NewGuid(),
            Status = status,
            FeasibilityStatus = ProductionFeasibilityStatus.PENDING,
            MaterialAvailable = materialAvailable,
            SubmittedForReviewAt = submittedForReviewAt,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = submittedForReviewAt,
            ProductVersion = versionProduct
        };
    }

    private static async Task<QueueGraphSeed> SeedQueueGraphAsync(AppDbContext context)
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
        return new QueueGraphSeed(project, proposal, sourceVersion, request);
    }

    private sealed record QueueGraphSeed(
        Project Project,
        Proposal Proposal,
        ProductVersion ProductVersion,
        CustomizationRequest Request);

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }
}
