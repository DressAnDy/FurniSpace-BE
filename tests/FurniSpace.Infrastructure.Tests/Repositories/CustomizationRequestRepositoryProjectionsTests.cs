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
            SourceProductVersionId = Guid.NewGuid(),
            RequestedByCustomerId = Guid.NewGuid(),
            RequestTitle = "Change material",
            RequestDescription = "Use darker oak",
            RequestedMaterial = "Oak",
            RequestedColor = "Brown",
            AcceptedRequestVersionId = Guid.NewGuid(),
            Status = CustomizationStatus.REVIEWING,
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
        Assert.Equal(source.SourceProductVersionId, detail.SourceProductVersionId);
        Assert.Equal(source.RequestTitle, detail.RequestTitle);
        Assert.Equal(source.RequestDescription, detail.RequestDescription);
        Assert.Equal(source.RequestedMaterial, detail.RequestedMaterial);
        Assert.Equal(source.Status, detail.Status);
        Assert.Equal(source.ProjectName, detail.ProjectName);
        Assert.Equal(source.AssignedDesignerId, detail.AssignedDesignerId);
    }
}
