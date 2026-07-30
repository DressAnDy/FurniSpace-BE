using System;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.CustomizationRequests;
using FurniSpace.Infrastructure.Repositories.Repository;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.Repositories;

public sealed class CustomizationRequestRepositoryProjectionsTests
{
    [Fact]
    public void ToDetailReadModel_CopiesAllReadableFields()
    {
        var requestId = Guid.NewGuid();
        var source = new CustomizationRequestReadModel
        {
            CustomizationRequestId = requestId,
            ProjectId = Guid.NewGuid(),
            ProposalId = Guid.NewGuid(),
            ProductVersionId = Guid.NewGuid(),
            RequestedByCustomerId = Guid.NewGuid(),
            RequestTitle = "Change material",
            RequestDescription = "Use darker oak",
            RequestedMaterial = "Oak",
            RequestedColor = "Brown",
            DesignerSpecNote = "Possible",
            ProductionReviewBy = Guid.NewGuid(),
            FeasibilityNote = "Feasible",
            EstimatedProductionDays = 5,
            EstimatedAdditionalCost = 250000m,
            AdditionalCostReason = "Custom finish",
            MaterialAvailable = true,
            ProductionRiskNote = "Low risk",
            ApprovedProductVersionId = Guid.NewGuid(),
            Status = CustomizationStatus.PRODUCTION_REVIEWING,
            CustomerAcceptedAt = DateTime.UtcNow,
            CustomerRejectedAt = null,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow,
            CustomerId = Guid.NewGuid(),
            ProjectName = "Cafe Project",
            AssignedSalesId = Guid.NewGuid(),
            AssignedDesignerId = Guid.NewGuid()
        };

        var detail = CustomizationRequestRepositoryProjections.ToDetailReadModel(source);

        Assert.Equal(requestId, detail.CustomizationRequestId);
        Assert.Equal(source.ProjectId, detail.ProjectId);
        Assert.Equal(source.ProposalId, detail.ProposalId);
        Assert.Equal(source.ProductVersionId, detail.ProductVersionId);
        Assert.Equal(source.RequestTitle, detail.RequestTitle);
        Assert.Equal(source.RequestDescription, detail.RequestDescription);
        Assert.Equal(source.RequestedMaterial, detail.RequestedMaterial);
        Assert.Equal(source.DesignerSpecNote, detail.DesignerSpecNote);
        Assert.Equal(source.EstimatedAdditionalCost, detail.EstimatedAdditionalCost);
        Assert.Equal(source.Status, detail.Status);
        Assert.Equal(source.ProjectName, detail.ProjectName);
        Assert.Equal(source.AssignedDesignerId, detail.AssignedDesignerId);
    }
}
