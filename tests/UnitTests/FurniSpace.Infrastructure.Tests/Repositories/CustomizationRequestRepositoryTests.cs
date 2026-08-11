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

public sealed class CustomizationRequestRepositoryTests
{
    [Fact]
    public async Task HasPendingForProposalAsync_WithPendingStatus_ReturnsTrue()
    {
        await using var context = CreateContext();
        var proposalId = Guid.NewGuid();
        context.CustomizationRequestSet.Add(CreateRequest(proposalId, CustomizationStatus.REVIEWING));
        context.CustomizationRequestSet.Add(CreateRequest(proposalId, CustomizationStatus.CANCELLED));
        await context.SaveChangesAsync();
        var repository = new CustomizationRequestRepository(context);

        var result = await repository.HasPendingForProposalAsync(proposalId);

        Assert.True(result);
    }

    [Fact]
    public async Task HasPendingForProposalAsync_WithOnlyResolvedStatuses_ReturnsFalse()
    {
        await using var context = CreateContext();
        var proposalId = Guid.NewGuid();
        context.CustomizationRequestSet.Add(CreateRequest(proposalId, CustomizationStatus.ACCEPTED));
        context.CustomizationRequestSet.Add(CreateRequest(proposalId, CustomizationStatus.CANCELLED));
        context.CustomizationRequestSet.Add(CreateRequest(proposalId, CustomizationStatus.CANCELLED));
        await context.SaveChangesAsync();
        var repository = new CustomizationRequestRepository(context);

        var result = await repository.HasPendingForProposalAsync(proposalId);

        Assert.False(result);
    }

    [Fact]
    public async Task HasActiveRequestForProductVersionAsync_WithActiveStatus_ReturnsTrue()
    {
        await using var context = CreateContext();
        var projectId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var productVersionId = Guid.NewGuid();
        context.CustomizationRequestSet.Add(CreateRequest(
            projectId,
            proposalId,
            productVersionId,
            CustomizationStatus.SUBMITTED));
        context.CustomizationRequestSet.Add(CreateRequest(
            projectId,
            proposalId,
            productVersionId,
            CustomizationStatus.ACCEPTED));
        await context.SaveChangesAsync();
        var repository = new CustomizationRequestRepository(context);

        var result = await repository.HasActiveRequestForProductVersionAsync(
            projectId,
            proposalId,
            productVersionId);

        Assert.True(result);
    }

    [Fact]
    public async Task HasActiveRequestForProductVersionAsync_WithOnlyResolvedStatuses_ReturnsFalse()
    {
        await using var context = CreateContext();
        var projectId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var productVersionId = Guid.NewGuid();
        context.CustomizationRequestSet.Add(CreateRequest(
            projectId,
            proposalId,
            productVersionId,
            CustomizationStatus.ACCEPTED));
        context.CustomizationRequestSet.Add(CreateRequest(
            projectId,
            proposalId,
            productVersionId,
            CustomizationStatus.CANCELLED));
        await context.SaveChangesAsync();
        var repository = new CustomizationRequestRepository(context);

        var result = await repository.HasActiveRequestForProductVersionAsync(
            projectId,
            proposalId,
            productVersionId);

        Assert.False(result);
    }

    [Fact]
    public async Task GetSubmitContextAsync_ReturnsProjectCodeAndProductVersionId()
    {
        await using var context = CreateContext();
        var productVersionId = Guid.NewGuid();
        var project = new Project
        {
            ProjectId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
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
        var proposalItem = new ProposalItem
        {
            ProposalItemId = Guid.NewGuid(),
            ProposalId = proposal.ProposalId,
            ProductVersionId = productVersionId,
            ItemName = "Dining Chair"
        };
        context.ProjectSet.Add(project);
        context.ProposalSet.Add(proposal);
        context.ProposalItemSet.Add(proposalItem);
        await context.SaveChangesAsync();
        var repository = new CustomizationRequestRepository(context);

        var result = await repository.GetSubmitContextAsync(proposalItem.ProposalItemId);

        Assert.NotNull(result);
        Assert.Equal("PRJ-000001", result!.ProjectCode);
        Assert.Equal(project.ProjectId, result.ProjectId);
        Assert.Equal(proposal.ProposalId, result.ProposalId);
        Assert.Equal(productVersionId, result.ProductVersionId);
    }

    [Fact]
    public async Task GetByProjectAsync_FiltersByProposalProductVersionAndStatus()
    {
        await using var context = CreateContext();
        var graph = await SeedRequestGraphAsync(context);
        var otherProposal = new Proposal
        {
            ProposalId = Guid.NewGuid(),
            ProjectId = graph.Project.ProjectId,
            ProposalName = "Other Proposal",
            Status = ProposalStatus.PUBLISHED
        };
        var otherVersion = new ProductVersion
        {
            ProductVersionId = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            VersionCode = "PV-OTHER",
            VersionName = "Other Chair"
        };
        context.ProposalSet.Add(otherProposal);
        context.ProductVersionSet.Add(otherVersion);
        context.CustomizationRequestSet.Add(CreateStoredRequest(
            graph,
            CustomizationStatus.SUBMITTED));
        context.CustomizationRequestSet.Add(new CustomizationRequest
        {
            CustomizationRequestId = Guid.NewGuid(),
            ProjectId = graph.Project.ProjectId,
            ProposalId = otherProposal.ProposalId,
            SourceProductVersionId = otherVersion.ProductVersionId,
            RequestTitle = "Other request",
            Status = CustomizationStatus.SUBMITTED
        });
        context.CustomizationRequestSet.Add(new CustomizationRequest
        {
            CustomizationRequestId = Guid.NewGuid(),
            ProjectId = graph.Project.ProjectId,
            ProposalId = graph.Proposal.ProposalId,
            SourceProductVersionId = graph.ProductVersion.ProductVersionId,
            RequestTitle = "Accepted request",
            Status = CustomizationStatus.ACCEPTED
        });
        await context.SaveChangesAsync();
        var repository = new CustomizationRequestRepository(context);

        var items = await repository.GetByProjectAsync(new CustomizationRequestQueryReadModel
        {
            ProjectId = graph.Project.ProjectId,
            ProposalId = graph.Proposal.ProposalId,
            SourceProductVersionId = graph.ProductVersion.ProductVersionId,
            Status = CustomizationStatus.SUBMITTED
        });

        Assert.Single(items);
        Assert.Equal("Change material", items[0].RequestTitle);
        Assert.Equal(graph.Project.CustomerId, items[0].CustomerId);
        Assert.Equal(graph.Project.ProjectName, items[0].ProjectName);
    }

    [Fact]
    public async Task GetDetailAsync_ReturnsSourceProductVersion()
    {
        await using var context = CreateContext();
        var graph = await SeedRequestGraphAsync(context);
        var request = CreateStoredRequest(graph, CustomizationStatus.REVIEWING);
        context.CustomizationRequestSet.Add(request);
        await context.SaveChangesAsync();
        var repository = new CustomizationRequestRepository(context);

        var detail = await repository.GetDetailAsync(request.CustomizationRequestId);

        Assert.NotNull(detail);
        Assert.Equal(request.CustomizationRequestId, detail!.CustomizationRequestId);
        Assert.Equal(graph.ProductVersion.ProductVersionId, detail.SourceProductVersion.ProductVersionId);
        Assert.Equal("Dining Chair", detail.SourceProductVersion.VersionName);
    }

    [Fact]
    public async Task GetDetailAsync_WhenRequestMissing_ReturnsNull()
    {
        await using var context = CreateContext();
        var repository = new CustomizationRequestRepository(context);

        var detail = await repository.GetDetailAsync(Guid.NewGuid());

        Assert.Null(detail);
    }

    [Fact]
    public async Task GetSubmitContextAsync_WhenProposalItemMissing_ReturnsNull()
    {
        await using var context = CreateContext();
        var repository = new CustomizationRequestRepository(context);

        var result = await repository.GetSubmitContextAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task HasQuotationForProposalAsync_WhenQuotationExists_ReturnsTrue()
    {
        await using var context = CreateContext();
        var proposalId = Guid.NewGuid();
        context.QuotationSet.Add(new Quotation
        {
            QuotationId = Guid.NewGuid(),
            ProposalId = proposalId,
            ProjectId = Guid.NewGuid(),
            QuotationCode = "QT-001",
            SubtotalAmount = 100m,
            TotalDiscountAmount = 0m,
            PreVatAmount = 100m,
            VatRate = 0.08m,
            VatAmount = 8m,
            TotalAmount = 100m,
            Currency = "VND",
            Status = QuotationStatus.DRAFT
        });
        await context.SaveChangesAsync();
        var repository = new CustomizationRequestRepository(context);

        var result = await repository.HasQuotationForProposalAsync(proposalId);

        Assert.True(result);
    }

    [Fact]
    public async Task HasProductionVisibleRequestAsync_WhenReviewingVersionExists_ReturnsTrue()
    {
        await using var context = CreateContext();
        var graph = await SeedRequestGraphAsync(context);
        var request = CreateStoredRequest(graph, CustomizationStatus.REVIEWING);
        context.CustomizationRequestSet.Add(request);
        var versionProduct = new ProductVersion
        {
            ProductVersionId = Guid.NewGuid(),
            ProductId = graph.ProductVersion.ProductId,
            ProjectId = graph.Project.ProjectId,
            VersionCode = "PV-CUST-001",
            VersionName = "Custom",
            VersionType = ProductVersionType.PROJECT_SPECIFIC,
            Status = ProductStatus.ACTIVE
        };
        context.ProductVersionSet.Add(versionProduct);
        context.CustomizationRequestVersionSet.Add(new CustomizationRequestVersion
        {
            CustomizationRequestVersionId = Guid.NewGuid(),
            CustomizationRequestId = request.CustomizationRequestId,
            ProductVersionId = versionProduct.ProductVersionId,
            VersionNo = 1,
            CreatedByDesignerId = graph.Project.AssignedDesignerId ?? Guid.NewGuid(),
            Status = CustomizationVersionStatus.REVIEWING,
            FeasibilityStatus = ProductionFeasibilityStatus.PENDING,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        var repository = new CustomizationRequestRepository(context);

        var result = await repository.HasProductionVisibleRequestAsync(
            graph.Project.ProjectId,
            Guid.NewGuid());

        Assert.True(result);
    }

    [Fact]
    public async Task HasProductionVisibleRequestAsync_WhenNoReviewingVersion_ReturnsFalse()
    {
        await using var context = CreateContext();
        var graph = await SeedRequestGraphAsync(context);
        context.CustomizationRequestSet.Add(CreateStoredRequest(graph, CustomizationStatus.SUBMITTED));
        await context.SaveChangesAsync();
        var repository = new CustomizationRequestRepository(context);

        var result = await repository.HasProductionVisibleRequestAsync(
            graph.Project.ProjectId,
            Guid.NewGuid());

        Assert.False(result);
    }

    private static async Task<RequestGraphSeed> SeedRequestGraphAsync(AppDbContext context)
    {
        var project = new Project
        {
            ProjectId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            AssignedSalesId = Guid.NewGuid(),
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
        var productVersion = new ProductVersion
        {
            ProductVersionId = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            VersionCode = "PV-TEST-001",
            VersionName = "Dining Chair",
            Material = "Oak",
            Status = ProductStatus.ACTIVE
        };
        context.ProjectSet.Add(project);
        context.ProposalSet.Add(proposal);
        context.ProductVersionSet.Add(productVersion);
        await context.SaveChangesAsync();
        return new RequestGraphSeed(project, proposal, productVersion);
    }

    private static CustomizationRequest CreateStoredRequest(
        RequestGraphSeed graph,
        CustomizationStatus status) => new()
    {
        CustomizationRequestId = Guid.NewGuid(),
        ProjectId = graph.Project.ProjectId,
        ProposalId = graph.Proposal.ProposalId,
        SourceProductVersionId = graph.ProductVersion.ProductVersionId,
        RequestTitle = "Change material",
        Status = status,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private sealed record RequestGraphSeed(Project Project, Proposal Proposal, ProductVersion ProductVersion);

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static CustomizationRequest CreateRequest(
        Guid proposalId,
        CustomizationStatus status) => new()
    {
        CustomizationRequestId = Guid.NewGuid(),
        ProjectId = Guid.NewGuid(),
        ProposalId = proposalId,
        SourceProductVersionId = Guid.NewGuid(),
        RequestTitle = "Change material",
        Status = status
    };

    private static CustomizationRequest CreateRequest(
        Guid projectId,
        Guid proposalId,
        Guid productVersionId,
        CustomizationStatus status) => new()
    {
        CustomizationRequestId = Guid.NewGuid(),
        ProjectId = projectId,
        ProposalId = proposalId,
        SourceProductVersionId = productVersionId,
        RequestTitle = "Change material",
        Status = status
    };
}
