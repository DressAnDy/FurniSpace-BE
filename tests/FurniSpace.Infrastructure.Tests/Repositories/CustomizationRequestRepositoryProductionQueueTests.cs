#nullable enable

using System;
using System.Linq;
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
    public async Task GetProductionQueueAsync_FiltersByStatusAndOrdersByUpdatedAtDescending()
    {
        await using var context = CreateContext();
        var data = await SeedQueueGraphAsync(context);
        var older = CreateQueueRequest(
            data,
            CustomizationStatus.PRODUCTION_REVIEWING,
            updatedAt: DateTime.UtcNow.AddHours(-2));
        var newer = CreateQueueRequest(
            data,
            CustomizationStatus.PRODUCTION_REVIEWING,
            updatedAt: DateTime.UtcNow.AddHours(-1));
        context.CustomizationRequestSet.AddRange(older, newer);
        context.CustomizationRequestSet.Add(CreateQueueRequest(
            data,
            CustomizationStatus.ACCEPTED,
            updatedAt: DateTime.UtcNow));
        await context.SaveChangesAsync();
        var repository = new CustomizationRequestRepository(context);

        var items = await repository.GetProductionQueueAsync(new ProductionCustomizationRequestQueueQueryReadModel
        {
            Statuses = [CustomizationStatus.PRODUCTION_REVIEWING],
            Page = 1,
            PageSize = 10
        });

        Assert.Equal(2, items.Count);
        Assert.Contains(items, item => item.Request.CustomizationRequestId == newer.CustomizationRequestId);
        Assert.Contains(items, item => item.Request.CustomizationRequestId == older.CustomizationRequestId);
        Assert.True(items[0].Request.UpdatedAt >= items[1].Request.UpdatedAt);
        Assert.Equal("Cafe Proposal", items[0].ProposalName);
        Assert.Equal("Dining Chair", items[0].SourceProductVersion.VersionName);
        Assert.Equal(data.Project.ProjectName, items[0].Request.ProjectName);
    }

    [Fact]
    public async Task CountProductionQueueAsync_AppliesProjectProposalAndMaterialFilters()
    {
        await using var context = CreateContext();
        var data = await SeedQueueGraphAsync(context);
        var matching = CreateQueueRequest(
            data,
            CustomizationStatus.PRODUCTION_REVIEWING,
            materialAvailable: true,
            updatedAt: DateTime.UtcNow.AddMinutes(-5));
        var otherProject = new Project
        {
            ProjectId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            ProjectName = "Other Project"
        };
        context.ProjectSet.Add(otherProject);
        var otherProposal = new Proposal
        {
            ProposalId = Guid.NewGuid(),
            ProjectId = otherProject.ProjectId,
            ProposalName = "Other Proposal",
            Status = ProposalStatus.PUBLISHED
        };
        context.ProposalSet.Add(otherProposal);
        var otherVersion = new ProductVersion
        {
            ProductVersionId = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            VersionCode = "PV-TEST-002",
            VersionName = "Other Chair"
        };
        context.ProductVersionSet.Add(otherVersion);
        context.CustomizationRequestSet.Add(matching);
        context.CustomizationRequestSet.Add(new CustomizationRequest
        {
            CustomizationRequestId = Guid.NewGuid(),
            ProjectId = otherProject.ProjectId,
            ProposalId = otherProposal.ProposalId,
            ProductVersionId = otherVersion.ProductVersionId,
            RequestTitle = "Other request",
            Status = CustomizationStatus.PRODUCTION_REVIEWING,
            MaterialAvailable = false,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        var repository = new CustomizationRequestRepository(context);

        var count = await repository.CountProductionQueueAsync(new ProductionCustomizationRequestQueueQueryReadModel
        {
            Statuses = [CustomizationStatus.PRODUCTION_REVIEWING],
            ProjectId = data.Project.ProjectId,
            ProposalId = data.Proposal.ProposalId,
            MaterialAvailable = true,
            FromDate = DateTime.UtcNow.AddHours(-1),
            ToDate = DateTime.UtcNow.AddHours(1)
        });

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task GetProductionQueueAsync_AppliesPagination()
    {
        await using var context = CreateContext();
        var data = await SeedQueueGraphAsync(context);
        context.CustomizationRequestSet.AddRange(
            CreateQueueRequest(data, CustomizationStatus.PRODUCTION_REVIEWING, updatedAt: DateTime.UtcNow.AddMinutes(-3)),
            CreateQueueRequest(data, CustomizationStatus.PRODUCTION_REVIEWING, updatedAt: DateTime.UtcNow.AddMinutes(-2)),
            CreateQueueRequest(data, CustomizationStatus.PRODUCTION_REVIEWING, updatedAt: DateTime.UtcNow.AddMinutes(-1)));
        await context.SaveChangesAsync();
        var repository = new CustomizationRequestRepository(context);

        var items = await repository.GetProductionQueueAsync(new ProductionCustomizationRequestQueueQueryReadModel
        {
            Statuses = [CustomizationStatus.PRODUCTION_REVIEWING],
            Page = 2,
            PageSize = 1
        });

        Assert.Single(items);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<QueueSeedData> SeedQueueGraphAsync(AppDbContext context)
    {
        var project = new Project
        {
            ProjectId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            AssignedSalesId = Guid.NewGuid(),
            AssignedDesignerId = Guid.NewGuid(),
            ProjectName = "Cafe Project"
        };
        var proposal = new Proposal
        {
            ProposalId = Guid.NewGuid(),
            ProjectId = project.ProjectId,
            ProposalName = "Cafe Proposal",
            Status = ProposalStatus.PUBLISHED
        };
        var productVersion = new ProductVersion
        {
            ProductVersionId = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            VersionCode = "PV-TEST-001",
            VersionName = "Dining Chair",
            Material = "Oak",
            Color = "Natural",
            Width = 45m,
            Height = 90m,
            Depth = 50m,
            EstimatedPrice = 1000000m
        };
        context.ProjectSet.Add(project);
        context.ProposalSet.Add(proposal);
        context.ProductVersionSet.Add(productVersion);
        await context.SaveChangesAsync();
        return new QueueSeedData(project, proposal, productVersion);
    }

    private static CustomizationRequest CreateQueueRequest(
        QueueSeedData data,
        CustomizationStatus status,
        bool? materialAvailable = null,
        DateTime? updatedAt = null) => new()
    {
        CustomizationRequestId = Guid.NewGuid(),
        ProjectId = data.Project.ProjectId,
        ProposalId = data.Proposal.ProposalId,
        ProductVersionId = data.ProductVersion.ProductVersionId,
        RequestTitle = "Change material",
        RequestDescription = "Use darker oak",
        RequestedMaterial = "Dark oak",
        Status = status,
        MaterialAvailable = materialAvailable,
        UpdatedAt = updatedAt ?? DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow.AddDays(-1)
    };

    private sealed record QueueSeedData(Project Project, Proposal Proposal, ProductVersion ProductVersion);
}
