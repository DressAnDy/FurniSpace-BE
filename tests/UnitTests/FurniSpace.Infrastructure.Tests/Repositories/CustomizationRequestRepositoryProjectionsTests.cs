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

    [Fact]
    public void ToDetailReadModel_CopiesDimensionFields()
    {
        var source = new CustomizationRequestReadModel
        {
            CustomizationRequestId = Guid.NewGuid(),
            RequestedWidth = 60m,
            RequestedHeight = 90m,
            RequestedDepth = 45m,
            RequestedChangeNote = "Rounded corners"
        };

        var detail = CustomizationRequestRepositoryProjections.ToDetailReadModel(source);

        Assert.Equal(60m, detail.RequestedWidth);
        Assert.Equal(90m, detail.RequestedHeight);
        Assert.Equal(45m, detail.RequestedDepth);
        Assert.Equal("Rounded corners", detail.RequestedChangeNote);
    }

    [Fact]
    public async Task RequestProjectReadModel_ProjectsJoinedProjectFields()
    {
        await using var context = CreateContext();
        var project = new Project
        {
            ProjectId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            AssignedSalesId = Guid.NewGuid(),
            AssignedDesignerId = Guid.NewGuid(),
            ProjectName = "Showroom Project",
            ProjectCode = "PRJ-000002",
            Status = ProjectStatus.PROPOSAL_CONSULTING
        };
        var request = new CustomizationRequest
        {
            CustomizationRequestId = Guid.NewGuid(),
            ProjectId = project.ProjectId,
            ProposalId = Guid.NewGuid(),
            SourceProductVersionId = Guid.NewGuid(),
            RequestTitle = "Adjust dimensions",
            Status = CustomizationStatus.SUBMITTED
        };
        context.ProjectSet.Add(project);
        context.CustomizationRequestSet.Add(request);
        await context.SaveChangesAsync();

        var readModel = await context.CustomizationRequestSet
            .Join(
                context.ProjectSet,
                item => item.ProjectId,
                itemProject => itemProject.ProjectId,
                (item, itemProject) => new CustomizationRequestRepositoryProjections.RequestProjectJoin
                {
                    Request = item,
                    Project = itemProject
                })
            .Select(CustomizationRequestRepositoryProjections.RequestProjectReadModel)
            .SingleAsync();

        Assert.Equal(request.CustomizationRequestId, readModel.CustomizationRequestId);
        Assert.Equal(project.CustomerId, readModel.CustomerId);
        Assert.Equal("Showroom Project", readModel.ProjectName);
        Assert.Equal(project.AssignedDesignerId, readModel.AssignedDesignerId);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }
}
