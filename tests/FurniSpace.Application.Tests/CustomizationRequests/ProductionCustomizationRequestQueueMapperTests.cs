#nullable enable

using System;
using FurniSpace.Application.Common.CustomizationRequests;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.CustomizationRequests;
using Xunit;

namespace FurniSpace.Application.Tests.CustomizationRequests;

public sealed class ProductionCustomizationRequestQueueMapperTests
{
    public ProductionCustomizationRequestQueueMapperTests()
    {
        MapsterTestSetup.EnsureConfigured();
    }

    [Fact]
    public void ToDto_MapsRequestAndSummaries()
    {
        var projectId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var proposalItemId = Guid.NewGuid();
        var readModel = new ProductionCustomizationRequestQueueReadModel
        {
            Request = new CustomizationRequestReadModel
            {
                CustomizationRequestId = Guid.NewGuid(),
                ProjectId = projectId,
                ProposalId = proposalId,
                ProposalItemId = proposalItemId,
                RequestTitle = "Change material",
                RequestDescription = "Use darker oak",
                RequestedMaterial = "Dark oak",
                Status = CustomizationStatus.PRODUCTION_REVIEWING,
                ProjectName = "Cafe Project",
                CustomerId = Guid.NewGuid()
            },
            ProposalName = "Cafe Proposal",
            ProposalStatus = ProposalStatus.PUBLISHED,
            ProposalItem = new()
            {
                ProposalItemId = proposalItemId,
                ProposalId = proposalId,
                ItemName = "Dining Chair",
                ItemType = "PRODUCT_ITEM",
                Quantity = 2,
                Width = 45m,
                Height = 90m,
                Depth = 50m,
                Material = "Oak",
                Color = "Natural",
                UnitPriceSnapshot = 1000000m,
                TotalPriceSnapshot = 2000000m
            }
        };

        var dto = ProductionCustomizationRequestQueueMapper.ToDto(readModel);

        Assert.Equal(readModel.Request.RequestTitle, dto.RequestTitle);
        Assert.Equal(readModel.Request.RequestDescription, dto.RequestDescription);
        Assert.Equal(readModel.Request.RequestedMaterial, dto.RequestedMaterial);
        Assert.Equal(projectId, dto.Project.ProjectId);
        Assert.Equal("Cafe Project", dto.Project.ProjectName);
        Assert.Equal(proposalId, dto.Proposal.ProposalId);
        Assert.Equal("Cafe Proposal", dto.Proposal.ProposalName);
        Assert.Equal(proposalItemId, dto.ProposalItem.ProposalItemId);
        Assert.Equal("Dining Chair", dto.ProposalItem.ItemName);
        Assert.Equal(45m, dto.ProposalItem.Width);
        Assert.Equal(2000000m, dto.ProposalItem.TotalPriceSnapshot);
    }
}
