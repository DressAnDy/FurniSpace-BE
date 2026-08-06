#nullable enable

using System;
using FurniSpace.Application.Common.CustomizationRequests;
using FurniSpace.Domain.Enums;
using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.ReadModels.CustomizationRequests;
using Xunit;

namespace FurniSpace.Application.Tests.CustomizationRequests;

public sealed class ProductionCustomizationVersionQueueMapperTests
{
    public ProductionCustomizationVersionQueueMapperTests()
    {
        MapsterTestSetup.EnsureConfigured();
    }

    [Fact]
    public void ToDto_MapsVersionRequestAndSummaries()
    {
        var projectId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var sourceProductVersionId = Guid.NewGuid();
        var readModel = new ProductionCustomizationVersionQueueReadModel
        {
            Request = new CustomizationRequestReadModel
            {
                CustomizationRequestId = Guid.NewGuid(),
                ProjectId = projectId,
                ProposalId = proposalId,
                SourceProductVersionId = sourceProductVersionId,
                RequestTitle = "Change material",
                Status = CustomizationStatus.REVIEWING,
                ProjectName = "Cafe Project",
                CustomerId = Guid.NewGuid()
            },
            Version = new CustomizationRequestVersionReadModel
            {
                CustomizationRequestVersionId = Guid.NewGuid(),
                VersionNo = 1,
                Status = CustomizationVersionStatus.REVIEWING,
                FeasibilityStatus = ProductionFeasibilityStatus.PENDING,
                CreatedByDesignerId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                ProductVersion = new ProductVersion
                {
                    ProductVersionId = Guid.NewGuid(),
                    VersionName = "Custom Chair"
                }
            },
            ProposalName = "Cafe Proposal",
            ProposalStatus = ProposalStatus.PUBLISHED,
            SourceProductVersion = new ProductVersion
            {
                ProductVersionId = sourceProductVersionId,
                ProductId = Guid.NewGuid(),
                VersionName = "Dining Chair",
                EstimatedPrice = 1000000m
            }
        };

        var dto = ProductionCustomizationVersionQueueMapper.ToDto(readModel);

        Assert.Equal(readModel.Request.RequestTitle, dto.Request.RequestTitle);
        Assert.Equal(projectId, dto.Project.ProjectId);
        Assert.Equal("Cafe Project", dto.Project.ProjectName);
        Assert.Equal(proposalId, dto.Proposal.ProposalId);
        Assert.Equal(sourceProductVersionId, dto.SourceProductVersion.ProductVersionId);
        Assert.Equal("Custom Chair", dto.Version.ProductVersion.VersionName);
    }
}
