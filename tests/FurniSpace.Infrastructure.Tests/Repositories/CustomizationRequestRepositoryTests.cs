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
        ProposalItemId = Guid.NewGuid(),
        RequestTitle = "Change material",
        Status = status
    };
}
