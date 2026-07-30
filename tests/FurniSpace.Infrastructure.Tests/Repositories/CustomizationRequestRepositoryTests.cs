using System;
using System.Threading.Tasks;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
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
        context.CustomizationRequestSet.Add(CreateRequest(proposalId, CustomizationStatus.PRODUCTION_REVIEWING));
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
        context.CustomizationRequestSet.Add(CreateRequest(proposalId, CustomizationStatus.REJECTED_BY_CUSTOMER));
        context.CustomizationRequestSet.Add(CreateRequest(proposalId, CustomizationStatus.NOT_FEASIBLE));
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
            CustomizationStatus.REJECTED_BY_CUSTOMER));
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
        ProductVersionId = Guid.NewGuid(),
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
        ProductVersionId = productVersionId,
        RequestTitle = "Change material",
        Status = status
    };
}
