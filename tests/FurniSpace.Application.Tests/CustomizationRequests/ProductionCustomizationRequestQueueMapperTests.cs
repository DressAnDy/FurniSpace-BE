#nullable enable

using System;
using FurniSpace.Application.Common.CustomizationRequests;
using FurniSpace.Domain.Enums;
using FurniSpace.Domain.Entities;
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
        var productVersionId = Guid.NewGuid();
        var readModel = new ProductionCustomizationRequestQueueReadModel
        {
            Request = new CustomizationRequestReadModel
            {
                CustomizationRequestId = Guid.NewGuid(),
                ProjectId = projectId,
                ProposalId = proposalId,
                ProductVersionId = productVersionId,
                RequestTitle = "Change material",
                RequestDescription = "Use darker oak",
                RequestedMaterial = "Dark oak",
                Status = CustomizationStatus.PRODUCTION_REVIEWING,
                ProjectName = "Cafe Project",
                CustomerId = Guid.NewGuid()
            },
            ProposalName = "Cafe Proposal",
            ProposalStatus = ProposalStatus.PUBLISHED,
            SourceProductVersion = new ProductVersion
            {
                ProductVersionId = productVersionId,
                ProductId = Guid.NewGuid(),
                VersionName = "Dining Chair",
                Material = "Oak",
                Color = "Natural",
                Width = 45m,
                Height = 90m,
                Depth = 50m,
                EstimatedPrice = 1000000m
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
        Assert.Equal(productVersionId, dto.SourceProductVersion.ProductVersionId);
        Assert.Equal("Dining Chair", dto.SourceProductVersion.VersionName);
        Assert.Equal(45m, dto.SourceProductVersion.Width);
        Assert.Equal(1000000m, dto.SourceProductVersion.EstimatedPrice);
    }
}
