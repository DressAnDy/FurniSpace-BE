#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.DTOs.Projects;
using FurniSpace.Application.Services.Projects;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.DTOs.Projects;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Xunit;

namespace FurniSpace.Application.Tests.Projects;

public sealed class ProjectServiceTests
{
    [Fact]
    public async Task RequestInformationAsync_WithAssignedSales_UpdatesProjectStatus()
    {
        var projectId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var project = new Project
        {
            ProjectId = projectId,
            CustomerId = Guid.NewGuid(),
            ProjectName = "Moc Coffee",
            AssignedSalesId = salesId,
            Status = ProjectStatus.IN_CONSULTATION
        };
        var repository = new FakeProjectRepository(roleName: "SALES", entities: [project]);
        var service = new ProjectService(repository);

        var result = await service.RequestInformationAsync(projectId, salesId, new RequestProjectInformationRequestDto
        {
            Message = "Please provide exact store dimensions."
        });

        Assert.Equal(200, result.Status);
        Assert.Equal("More information requested successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(projectId, result.Data.ProjectId);
        Assert.Equal(ProjectStatus.NEED_BASIC_INFORMATION, result.Data.Status);
        Assert.NotEqual(default, result.Data.RequestedAt);
        Assert.Equal(ProjectStatus.NEED_BASIC_INFORMATION, project.Status);
        Assert.Equal(result.Data.RequestedAt, project.UpdatedAt);
        Assert.Equal(1, repository.GetAccountRoleNameCallCount);
        Assert.Equal(1, repository.GetByIdCallCount);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task RequestInformationAsync_WithAdmin_AllowsUnassignedProject()
    {
        var projectId = Guid.NewGuid();
        var project = new Project
        {
            ProjectId = projectId,
            CustomerId = Guid.NewGuid(),
            ProjectName = "Moc Coffee",
            Status = ProjectStatus.SUBMITTED
        };
        var repository = new FakeProjectRepository(roleName: "ADMIN", entities: [project]);
        var service = new ProjectService(repository);

        var result = await service.RequestInformationAsync(projectId, Guid.NewGuid(), new RequestProjectInformationRequestDto
        {
            Message = "Need more info"
        });

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(ProjectStatus.NEED_BASIC_INFORMATION, result.Data.Status);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task RequestInformationAsync_WithEmptyProjectId_ReturnsBadRequest()
    {
        var repository = new FakeProjectRepository(roleName: "SALES");
        var service = new ProjectService(repository);

        var result = await service.RequestInformationAsync(Guid.Empty, Guid.NewGuid(), new RequestProjectInformationRequestDto
        {
            Message = "Need more info"
        });

        Assert.Equal(400, result.Status);
        Assert.Equal("Project id is required.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetAccountRoleNameCallCount);
        Assert.Equal(0, repository.GetByIdCallCount);
    }

    [Fact]
    public async Task RequestInformationAsync_WithEmptyCurrentUser_ReturnsUnauthorized()
    {
        var repository = new FakeProjectRepository(roleName: "SALES");
        var service = new ProjectService(repository);

        var result = await service.RequestInformationAsync(Guid.NewGuid(), Guid.Empty, new RequestProjectInformationRequestDto
        {
            Message = "Need more info"
        });

        Assert.Equal(401, result.Status);
        Assert.Equal("Authenticated account id is required.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetAccountRoleNameCallCount);
        Assert.Equal(0, repository.GetByIdCallCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task RequestInformationAsync_WithMissingMessage_ReturnsBadRequest(string message)
    {
        var repository = new FakeProjectRepository(roleName: "SALES");
        var service = new ProjectService(repository);

        var result = await service.RequestInformationAsync(Guid.NewGuid(), Guid.NewGuid(), new RequestProjectInformationRequestDto
        {
            Message = message
        });

        Assert.Equal(400, result.Status);
        Assert.Equal("Request message is required.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetAccountRoleNameCallCount);
        Assert.Equal(0, repository.GetByIdCallCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("CUSTOMER")]
    [InlineData("DESIGNER")]
    public async Task RequestInformationAsync_WithUnsupportedRole_ReturnsForbidden(string? roleName)
    {
        var repository = new FakeProjectRepository(roleName: roleName);
        var service = new ProjectService(repository);

        var result = await service.RequestInformationAsync(Guid.NewGuid(), Guid.NewGuid(), new RequestProjectInformationRequestDto
        {
            Message = "Need more info"
        });

        Assert.Equal(403, result.Status);
        Assert.Equal("Only assigned sales or admin accounts can request more information.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(1, repository.GetAccountRoleNameCallCount);
        Assert.Equal(0, repository.GetByIdCallCount);
    }

    [Fact]
    public async Task RequestInformationAsync_WithMissingProject_ReturnsNotFound()
    {
        var repository = new FakeProjectRepository(roleName: "SALES");
        var service = new ProjectService(repository);

        var result = await service.RequestInformationAsync(Guid.NewGuid(), Guid.NewGuid(), new RequestProjectInformationRequestDto
        {
            Message = "Need more info"
        });

        Assert.Equal(404, result.Status);
        Assert.Equal("Project not found.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(1, repository.GetAccountRoleNameCallCount);
        Assert.Equal(1, repository.GetByIdCallCount);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task RequestInformationAsync_WithUnassignedSales_ReturnsForbidden()
    {
        var projectId = Guid.NewGuid();
        var project = new Project
        {
            ProjectId = projectId,
            CustomerId = Guid.NewGuid(),
            ProjectName = "Moc Coffee",
            AssignedSalesId = Guid.NewGuid(),
            Status = ProjectStatus.IN_CONSULTATION
        };
        var repository = new FakeProjectRepository(roleName: "SALES", entities: [project]);
        var service = new ProjectService(repository);

        var result = await service.RequestInformationAsync(projectId, Guid.NewGuid(), new RequestProjectInformationRequestDto
        {
            Message = "Need more info"
        });

        Assert.Equal(403, result.Status);
        Assert.Equal("You do not have access to request more information for this project.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task AssignSalesAsync_WithSalesRole_AssignsCurrentSalesAndStartsConsultation()
    {
        var projectId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var project = new Project
        {
            ProjectId = projectId,
            CustomerId = Guid.NewGuid(),
            ProjectName = "Moc Coffee",
            Status = ProjectStatus.SUBMITTED
        };
        var repository = new FakeProjectRepository(roleName: "SALES", entities: [project]);
        var service = new ProjectService(repository);

        var result = await service.AssignSalesAsync(projectId, salesId, new AssignProjectSalesRequestDto
        {
            Note = "Accepted for consultation."
        });

        Assert.Equal(200, result.Status);
        Assert.Equal("Project request accepted successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(projectId, result.Data.ProjectId);
        Assert.Equal(salesId, result.Data.AssignedSalesId);
        Assert.Equal(ProjectStatus.IN_CONSULTATION, result.Data.Status);
        Assert.NotNull(result.Data.SalesAssignedAt);
        Assert.Equal(salesId, project.AssignedSalesId);
        Assert.Equal(ProjectStatus.IN_CONSULTATION, project.Status);
        Assert.NotNull(project.SalesAssignedAt);
        Assert.Equal(1, repository.GetAccountRoleNameCallCount);
        Assert.Equal(1, repository.GetByIdCallCount);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task AssignSalesAsync_WithAdminRole_OverridesExistingSalesAssignment()
    {
        var projectId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var project = new Project
        {
            ProjectId = projectId,
            CustomerId = Guid.NewGuid(),
            ProjectName = "Moc Coffee",
            AssignedSalesId = Guid.NewGuid(),
            Status = ProjectStatus.NEED_BASIC_INFORMATION
        };
        var repository = new FakeProjectRepository(roleName: "ADMIN", entities: [project]);
        var service = new ProjectService(repository);

        var result = await service.AssignSalesAsync(projectId, adminId, new AssignProjectSalesRequestDto());

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(adminId, result.Data.AssignedSalesId);
        Assert.Equal(ProjectStatus.IN_CONSULTATION, result.Data.Status);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task AssignSalesAsync_WithSameSales_ReacceptsProject()
    {
        var projectId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var project = new Project
        {
            ProjectId = projectId,
            CustomerId = Guid.NewGuid(),
            ProjectName = "Moc Coffee",
            AssignedSalesId = salesId,
            Status = ProjectStatus.SUBMITTED
        };
        var repository = new FakeProjectRepository(roleName: "SALES", entities: [project]);
        var service = new ProjectService(repository);

        var result = await service.AssignSalesAsync(projectId, salesId, new AssignProjectSalesRequestDto());

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(salesId, result.Data.AssignedSalesId);
    }

    [Fact]
    public async Task AssignSalesAsync_WithEmptyProjectId_ReturnsBadRequest()
    {
        var repository = new FakeProjectRepository(roleName: "SALES");
        var service = new ProjectService(repository);

        var result = await service.AssignSalesAsync(Guid.Empty, Guid.NewGuid(), new AssignProjectSalesRequestDto());

        Assert.Equal(400, result.Status);
        Assert.Equal("Project id is required.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetAccountRoleNameCallCount);
        Assert.Equal(0, repository.GetByIdCallCount);
    }

    [Fact]
    public async Task AssignSalesAsync_WithEmptyCurrentUser_ReturnsUnauthorized()
    {
        var repository = new FakeProjectRepository(roleName: "SALES");
        var service = new ProjectService(repository);

        var result = await service.AssignSalesAsync(Guid.NewGuid(), Guid.Empty, new AssignProjectSalesRequestDto());

        Assert.Equal(401, result.Status);
        Assert.Equal("Authenticated account id is required.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetAccountRoleNameCallCount);
        Assert.Equal(0, repository.GetByIdCallCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("CUSTOMER")]
    [InlineData("DESIGNER")]
    public async Task AssignSalesAsync_WithUnsupportedRole_ReturnsForbidden(string? roleName)
    {
        var repository = new FakeProjectRepository(roleName: roleName);
        var service = new ProjectService(repository);

        var result = await service.AssignSalesAsync(Guid.NewGuid(), Guid.NewGuid(), new AssignProjectSalesRequestDto());

        Assert.Equal(403, result.Status);
        Assert.Equal("Only sales or admin accounts can accept project requests.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(1, repository.GetAccountRoleNameCallCount);
        Assert.Equal(0, repository.GetByIdCallCount);
    }

    [Fact]
    public async Task AssignSalesAsync_WithMissingProject_ReturnsNotFound()
    {
        var repository = new FakeProjectRepository(roleName: "SALES");
        var service = new ProjectService(repository);

        var result = await service.AssignSalesAsync(Guid.NewGuid(), Guid.NewGuid(), new AssignProjectSalesRequestDto());

        Assert.Equal(404, result.Status);
        Assert.Equal("Project not found.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(1, repository.GetAccountRoleNameCallCount);
        Assert.Equal(1, repository.GetByIdCallCount);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task AssignSalesAsync_WithInvalidStatus_ReturnsBadRequest()
    {
        var projectId = Guid.NewGuid();
        var project = new Project
        {
            ProjectId = projectId,
            CustomerId = Guid.NewGuid(),
            ProjectName = "Moc Coffee",
            Status = ProjectStatus.IN_CONSULTATION
        };
        var repository = new FakeProjectRepository(roleName: "SALES", entities: [project]);
        var service = new ProjectService(repository);

        var result = await service.AssignSalesAsync(projectId, Guid.NewGuid(), new AssignProjectSalesRequestDto());

        Assert.Equal(400, result.Status);
        Assert.Equal("Project cannot be accepted from its current status.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task AssignSalesAsync_WhenAssignedToAnotherSales_ReturnsConflict()
    {
        var projectId = Guid.NewGuid();
        var project = new Project
        {
            ProjectId = projectId,
            CustomerId = Guid.NewGuid(),
            ProjectName = "Moc Coffee",
            AssignedSalesId = Guid.NewGuid(),
            Status = ProjectStatus.SUBMITTED
        };
        var repository = new FakeProjectRepository(roleName: "SALES", entities: [project]);
        var service = new ProjectService(repository);

        var result = await service.AssignSalesAsync(projectId, Guid.NewGuid(), new AssignProjectSalesRequestDto());

        Assert.Equal(409, result.Status);
        Assert.Equal("Project is already assigned to another sales account.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task GetByIdAsync_WithAdminRole_ReturnsProjectDetail()
    {
        var projectId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var repository = new FakeProjectRepository(
            roleName: "ADMIN",
            detail: CreateProjectDetail(projectId, customerId));
        var service = new ProjectService(repository);

        var result = await service.GetByIdAsync(projectId, Guid.NewGuid());

        Assert.Equal(200, result.Status);
        Assert.Equal("Project detail retrieved successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(projectId, result.Data.ProjectId);
        Assert.Equal(customerId, result.Data.CustomerId);
        Assert.Equal("PRJ-2026-0001", result.Data.ProjectCode);
        Assert.Equal("Moc Coffee", result.Data.ProjectName);
        Assert.Equal("Cafe", result.Data.BusinessType);
        Assert.Equal("District 7", result.Data.ProjectAddress);
        Assert.Equal("Open cafe", result.Data.BusinessPurpose);
        Assert.Equal("Tables", result.Data.FurnitureRequirement);
        Assert.Equal("Warm design", result.Data.Description);
        Assert.Equal(80, result.Data.TotalAreaSqm);
        Assert.Equal(1, result.Data.NumberOfFloors);
        Assert.Equal(150000000, result.Data.BudgetMin);
        Assert.Equal(250000000, result.Data.BudgetMax);
        Assert.Equal(ProjectStatus.SUBMITTED, result.Data.Status);
        Assert.Equal(1, repository.GetDetailCallCount);
        Assert.Equal(1, repository.GetAccountRoleNameCallCount);
    }

    [Fact]
    public async Task GetByIdAsync_WithOwnerCustomer_ReturnsProjectDetail()
    {
        var projectId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var repository = new FakeProjectRepository(
            roleName: "CUSTOMER",
            detail: CreateProjectDetail(projectId, customerId));
        var service = new ProjectService(repository);

        var result = await service.GetByIdAsync(projectId, customerId);

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(customerId, result.Data.CustomerId);
    }

    [Fact]
    public async Task GetByIdAsync_WithAssignedSales_ReturnsProjectDetail()
    {
        var projectId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var detail = CreateProjectDetail(projectId, Guid.NewGuid());
        detail.AssignedSalesId = salesId;
        var repository = new FakeProjectRepository(roleName: "SALES", detail: detail);
        var service = new ProjectService(repository);

        var result = await service.GetByIdAsync(projectId, salesId);

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(salesId, result.Data.AssignedSalesId);
    }

    [Fact]
    public async Task GetByIdAsync_WithAssignedDesigner_ReturnsProjectDetail()
    {
        var projectId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var detail = CreateProjectDetail(projectId, Guid.NewGuid());
        detail.AssignedDesignerId = designerId;
        var repository = new FakeProjectRepository(roleName: "DESIGNER", detail: detail);
        var service = new ProjectService(repository);

        var result = await service.GetByIdAsync(projectId, designerId);

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(designerId, result.Data.AssignedDesignerId);
    }

    [Fact]
    public async Task GetByIdAsync_WithEmptyProjectId_ReturnsBadRequest()
    {
        var repository = new FakeProjectRepository(roleName: "ADMIN");
        var service = new ProjectService(repository);

        var result = await service.GetByIdAsync(Guid.Empty, Guid.NewGuid());

        Assert.Equal(400, result.Status);
        Assert.Equal("Project id is required.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetDetailCallCount);
    }

    [Fact]
    public async Task GetByIdAsync_WithEmptyCurrentUser_ReturnsUnauthorized()
    {
        var repository = new FakeProjectRepository(roleName: "ADMIN");
        var service = new ProjectService(repository);

        var result = await service.GetByIdAsync(Guid.NewGuid(), Guid.Empty);

        Assert.Equal(401, result.Status);
        Assert.Equal("Authenticated account id is required.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetDetailCallCount);
    }

    [Fact]
    public async Task GetByIdAsync_WithMissingProject_ReturnsNotFound()
    {
        var repository = new FakeProjectRepository(roleName: "ADMIN");
        var service = new ProjectService(repository);

        var result = await service.GetByIdAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(404, result.Status);
        Assert.Equal("Project not found.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(1, repository.GetDetailCallCount);
        Assert.Equal(0, repository.GetAccountRoleNameCallCount);
    }

    [Theory]
    [InlineData("CUSTOMER")]
    [InlineData("SALES")]
    [InlineData("DESIGNER")]
    [InlineData(null)]
    public async Task GetByIdAsync_WithUnauthorizedParticipant_ReturnsForbidden(string? roleName)
    {
        var projectId = Guid.NewGuid();
        var repository = new FakeProjectRepository(
            roleName: roleName,
            detail: CreateProjectDetail(projectId, Guid.NewGuid()));
        var service = new ProjectService(repository);

        var result = await service.GetByIdAsync(projectId, Guid.NewGuid());

        Assert.Equal(403, result.Status);
        Assert.Equal("You do not have access to view this project.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(1, repository.GetDetailCallCount);
        Assert.Equal(1, repository.GetAccountRoleNameCallCount);
    }

    [Fact]
    public async Task GetListAsync_WithSalesRole_ReturnsFilteredProjectQueue()
    {
        var salesId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var repository = new FakeProjectRepository(
            roleName: "SALES",
            listItems:
            [
                new ProjectListItemReadModel
                {
                    ProjectId = projectId,
                    ProjectCode = "PRJ-2026-0001",
                    ProjectName = "Moc Coffee",
                    BusinessType = "Cafe",
                    Status = ProjectStatus.SUBMITTED,
                    CustomerId = Guid.NewGuid(),
                    AssignedSalesId = salesId,
                    AssignedDesignerId = designerId,
                    SubmittedAt = DateTime.UtcNow
                }
            ]);
        var service = new ProjectService(repository);

        var result = await service.GetListAsync(Guid.NewGuid(), new ProjectListQueryDto
        {
            Status = ProjectStatus.SUBMITTED,
            AssignedSalesId = salesId,
            AssignedDesignerId = designerId,
            Search = "moc",
            Page = 1,
            Limit = 20
        });

        Assert.Equal(200, result.Status);
        Assert.Equal("Project request queue retrieved successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data.Page);
        Assert.Equal(20, result.Data.Limit);
        Assert.Equal(1, result.Data.Total);
        var item = Assert.Single(result.Data.Items);
        Assert.Equal(projectId, item.ProjectId);
        Assert.Equal("PRJ-2026-0001", item.ProjectCode);
        Assert.Equal("Moc Coffee", item.ProjectName);
        Assert.Equal(ProjectStatus.SUBMITTED, item.Status);
        Assert.Equal(salesId, item.AssignedSalesId);
        Assert.Equal(1, repository.GetAccountRoleNameCallCount);
        Assert.Equal(1, repository.GetListCallCount);
        Assert.Equal(1, repository.CountCallCount);
        Assert.Null(repository.LastListQuery!.CustomerId);
        Assert.Equal(ProjectStatus.SUBMITTED, repository.LastListQuery.Status);
        Assert.Equal(salesId, repository.LastListQuery.AssignedSalesId);
        Assert.Equal(designerId, repository.LastListQuery.AssignedDesignerId);
        Assert.Equal("moc", repository.LastListQuery.Search);
    }

    [Fact]
    public async Task GetListAsync_WithCustomerRole_RestrictsToCurrentCustomer()
    {
        var customerId = Guid.NewGuid();
        var repository = new FakeProjectRepository(roleName: "customer");
        var service = new ProjectService(repository);

        var result = await service.GetListAsync(customerId, new ProjectListQueryDto());

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data.Items);
        Assert.Equal(customerId, repository.LastListQuery!.CustomerId);
    }

    [Fact]
    public async Task GetListAsync_WithAdminRole_UsesDefaultPagination()
    {
        var repository = new FakeProjectRepository(roleName: "ADMIN");
        var service = new ProjectService(repository);

        var result = await service.GetListAsync(Guid.NewGuid(), new ProjectListQueryDto());

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data.Page);
        Assert.Equal(20, result.Data.Limit);
        Assert.Null(repository.LastListQuery!.CustomerId);
    }

    [Fact]
    public async Task GetListAsync_WithEmptyCurrentUser_ReturnsUnauthorized()
    {
        var repository = new FakeProjectRepository(roleName: "ADMIN");
        var service = new ProjectService(repository);

        var result = await service.GetListAsync(Guid.Empty, new ProjectListQueryDto());

        Assert.Equal(401, result.Status);
        Assert.Equal("Authenticated account id is required.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetAccountRoleNameCallCount);
        Assert.Equal(0, repository.GetListCallCount);
    }

    [Theory]
    [InlineData(0, 20, "Page must be greater than zero.")]
    [InlineData(1, 0, "Limit must be between 1 and 100.")]
    [InlineData(1, 101, "Limit must be between 1 and 100.")]
    public async Task GetListAsync_WithInvalidPagination_ReturnsBadRequest(int page, int limit, string message)
    {
        var repository = new FakeProjectRepository(roleName: "ADMIN");
        var service = new ProjectService(repository);

        var result = await service.GetListAsync(Guid.NewGuid(), new ProjectListQueryDto
        {
            Page = page,
            Limit = limit
        });

        Assert.Equal(400, result.Status);
        Assert.Equal(message, result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetAccountRoleNameCallCount);
        Assert.Equal(0, repository.GetListCallCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("DESIGNER")]
    public async Task GetListAsync_WithUnsupportedRole_ReturnsForbidden(string? roleName)
    {
        var repository = new FakeProjectRepository(roleName: roleName);
        var service = new ProjectService(repository);

        var result = await service.GetListAsync(Guid.NewGuid(), new ProjectListQueryDto());

        Assert.Equal(403, result.Status);
        Assert.Equal("You do not have access to view project requests.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(1, repository.GetAccountRoleNameCallCount);
        Assert.Equal(0, repository.GetListCallCount);
    }

    [Fact]
    public async Task CreateAsync_WithValidCustomerRequest_CreatesSubmittedProject()
    {
        var customerId = Guid.NewGuid();
        var repository = new FakeProjectRepository(roleName: "CUSTOMER", submittedCount: 3);
        var service = new ProjectService(repository);
        var targetDate = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(30);

        var result = await service.CreateAsync(customerId, new CreateProjectRequestDto
        {
            ProjectName = " Moc Coffee Interior Setup ",
            BusinessType = " Cafe ",
            ProjectAddress = " District 7, Ho Chi Minh City ",
            BusinessPurpose = " Open a small coffee shop ",
            FurnitureRequirement = " Need counter and tables ",
            Description = " Warm minimalist cafe interior ",
            TotalAreaSqm = 80,
            NumberOfFloors = 1,
            BudgetMin = 150000000,
            BudgetMax = 250000000,
            TargetCompletionDate = targetDate
        });

        Assert.Equal(201, result.Status);
        Assert.Equal("Project request submitted successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(customerId, result.Data.CustomerId);
        Assert.Equal("PRJ-" + DateTime.UtcNow.Year + "-0004", result.Data.ProjectCode);
        Assert.Equal("Moc Coffee Interior Setup", result.Data.ProjectName);
        Assert.Equal("Cafe", result.Data.BusinessType);
        Assert.Equal("District 7, Ho Chi Minh City", result.Data.ProjectAddress);
        Assert.Equal("Open a small coffee shop", result.Data.BusinessPurpose);
        Assert.Equal("Need counter and tables", result.Data.FurnitureRequirement);
        Assert.Equal("Warm minimalist cafe interior", result.Data.Description);
        Assert.Equal(80, result.Data.TotalAreaSqm);
        Assert.Equal(1, result.Data.NumberOfFloors);
        Assert.Equal(150000000, result.Data.BudgetMin);
        Assert.Equal(250000000, result.Data.BudgetMax);
        Assert.Equal(targetDate, result.Data.TargetCompletionDate);
        Assert.Equal(ProjectStatus.SUBMITTED, result.Data.Status);
        Assert.NotNull(result.Data.SubmittedAt);
        Assert.Null(result.Data.AssignedSalesId);
        Assert.Null(result.Data.AssignedDesignerId);
        Assert.Equal(1, repository.GetAccountRoleNameCallCount);
        Assert.Equal(1, repository.CountSubmittedInYearCallCount);
        Assert.Equal(1, repository.AddCallCount);
        Assert.Equal(1, repository.SaveChangesCallCount);
        var project = Assert.Single(repository.Projects);
        Assert.Equal(result.Data.ProjectId, project.ProjectId);
    }

    [Fact]
    public async Task CreateAsync_WithBlankOptionalFields_NormalizesToNull()
    {
        var repository = new FakeProjectRepository(roleName: "customer");
        var service = new ProjectService(repository);

        var result = await service.CreateAsync(Guid.NewGuid(), new CreateProjectRequestDto
        {
            ProjectName = "Project",
            BusinessType = "Cafe",
            FurnitureRequirement = "Tables",
            ProjectAddress = " ",
            BusinessPurpose = " ",
            Description = " "
        });

        Assert.Equal(201, result.Status);
        Assert.NotNull(result.Data);
        Assert.Null(result.Data.ProjectAddress);
        Assert.Null(result.Data.BusinessPurpose);
        Assert.Null(result.Data.Description);
    }

    [Fact]
    public async Task CreateAsync_WithEmptyCurrentUser_ReturnsUnauthorized()
    {
        var repository = new FakeProjectRepository(roleName: "CUSTOMER");
        var service = new ProjectService(repository);

        var result = await service.CreateAsync(Guid.Empty, ValidRequest());

        Assert.Equal(401, result.Status);
        Assert.Equal("Authenticated account id is required.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetAccountRoleNameCallCount);
        Assert.Equal(0, repository.AddCallCount);
    }

    [Fact]
    public async Task CreateAsync_WithMissingRequiredFields_ReturnsValidationErrors()
    {
        var repository = new FakeProjectRepository(roleName: "CUSTOMER");
        var service = new ProjectService(repository);

        var result = await service.CreateAsync(Guid.NewGuid(), new CreateProjectRequestDto
        {
            ProjectName = " ",
            BusinessType = " ",
            FurnitureRequirement = " "
        });

        Assert.Equal(400, result.Status);
        Assert.Equal("Validation failed", result.Message);
        Assert.Contains("Project name is required.", result.Errors!);
        Assert.Contains("Business type is required.", result.Errors!);
        Assert.Contains("Furniture requirement is required.", result.Errors!);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetAccountRoleNameCallCount);
    }

    [Fact]
    public async Task CreateAsync_WithNullRequiredFields_ReturnsValidationErrors()
    {
        var repository = new FakeProjectRepository(roleName: "CUSTOMER");
        var service = new ProjectService(repository);

        var result = await service.CreateAsync(Guid.NewGuid(), new CreateProjectRequestDto
        {
            ProjectName = null!,
            BusinessType = null!,
            FurnitureRequirement = null!
        });

        Assert.Equal(400, result.Status);
        Assert.Contains("Project name is required.", result.Errors!);
        Assert.Contains("Business type is required.", result.Errors!);
        Assert.Contains("Furniture requirement is required.", result.Errors!);
        Assert.Equal(0, repository.GetAccountRoleNameCallCount);
    }

    [Fact]
    public async Task CreateAsync_WithInvalidNumericAndDateFields_ReturnsValidationErrors()
    {
        var repository = new FakeProjectRepository(roleName: "CUSTOMER");
        var service = new ProjectService(repository);

        var result = await service.CreateAsync(Guid.NewGuid(), new CreateProjectRequestDto
        {
            ProjectName = new string('P', 151),
            BusinessType = new string('B', 101),
            FurnitureRequirement = "Tables",
            TotalAreaSqm = -1,
            NumberOfFloors = -1,
            BudgetMin = 10,
            BudgetMax = 5,
            TargetCompletionDate = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(-1)
        });

        Assert.Equal(400, result.Status);
        Assert.Contains("Project name must not exceed 150 characters.", result.Errors!);
        Assert.Contains("Business type must not exceed 100 characters.", result.Errors!);
        Assert.Contains("Total area must be greater than or equal to zero.", result.Errors!);
        Assert.Contains("Number of floors must be greater than or equal to zero.", result.Errors!);
        Assert.Contains("Minimum budget must be less than or equal to maximum budget.", result.Errors!);
        Assert.Contains("Target completion date must not be in the past.", result.Errors!);
        Assert.Equal(0, repository.GetAccountRoleNameCallCount);
    }

    [Fact]
    public async Task CreateAsync_WithNegativeBudgets_ReturnsValidationErrors()
    {
        var repository = new FakeProjectRepository(roleName: "CUSTOMER");
        var service = new ProjectService(repository);

        var result = await service.CreateAsync(Guid.NewGuid(), new CreateProjectRequestDto
        {
            ProjectName = "Project",
            BusinessType = "Cafe",
            FurnitureRequirement = "Tables",
            BudgetMin = -1,
            BudgetMax = -2
        });

        Assert.Equal(400, result.Status);
        Assert.Contains("Minimum budget must be greater than or equal to zero.", result.Errors!);
        Assert.Contains("Maximum budget must be greater than or equal to zero.", result.Errors!);
        Assert.Equal(0, repository.GetAccountRoleNameCallCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ADMIN")]
    [InlineData("SALES")]
    public async Task CreateAsync_WithNonCustomerRole_ReturnsForbidden(string? roleName)
    {
        var repository = new FakeProjectRepository(roleName: roleName);
        var service = new ProjectService(repository);

        var result = await service.CreateAsync(Guid.NewGuid(), ValidRequest());

        Assert.Equal(403, result.Status);
        Assert.Equal("Only customer accounts can submit project requests.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(1, repository.GetAccountRoleNameCallCount);
        Assert.Equal(0, repository.AddCallCount);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    private static CreateProjectRequestDto ValidRequest()
    {
        return new CreateProjectRequestDto
        {
            ProjectName = "Moc Coffee Interior Setup",
            BusinessType = "Cafe",
            FurnitureRequirement = "Need counter and tables",
            TargetCompletionDate = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(1)
        };
    }

    private static ProjectDetailReadModel CreateProjectDetail(Guid projectId, Guid customerId)
    {
        return new ProjectDetailReadModel
        {
            ProjectId = projectId,
            CustomerId = customerId,
            ProjectCode = "PRJ-2026-0001",
            ProjectName = "Moc Coffee",
            BusinessType = "Cafe",
            ProjectAddress = "District 7",
            BusinessPurpose = "Open cafe",
            FurnitureRequirement = "Tables",
            Description = "Warm design",
            TotalAreaSqm = 80,
            NumberOfFloors = 1,
            BudgetMin = 150000000,
            BudgetMax = 250000000,
            TargetCompletionDate = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(30),
            Status = ProjectStatus.SUBMITTED,
            SubmittedAt = DateTime.UtcNow
        };
    }

    private sealed class FakeProjectRepository : IProjectRepository
    {
        private readonly string? _roleName;
        private readonly int _submittedCount;
        private readonly IReadOnlyList<ProjectListItemReadModel> _listItems;
        private readonly ProjectDetailReadModel? _detail;
        private readonly List<Project> _projects = [];

        public FakeProjectRepository(
            string? roleName,
            int submittedCount = 0,
            IReadOnlyList<ProjectListItemReadModel>? listItems = null,
            ProjectDetailReadModel? detail = null,
            IReadOnlyList<Project>? entities = null)
        {
            _roleName = roleName;
            _submittedCount = submittedCount;
            _listItems = listItems ?? [];
            _detail = detail;
            _projects = entities?.ToList() ?? [];
        }

        public IReadOnlyList<Project> Projects => _projects;
        public int GetAccountRoleNameCallCount { get; private set; }
        public int CountSubmittedInYearCallCount { get; private set; }
        public int GetDetailCallCount { get; private set; }
        public int GetByIdCallCount { get; private set; }
        public int GetListCallCount { get; private set; }
        public int CountCallCount { get; private set; }
        public int AddCallCount { get; private set; }
        public int SaveChangesCallCount { get; private set; }
        public ProjectListQueryReadModel? LastListQuery { get; private set; }

        public Task<string?> GetAccountRoleNameAsync(Guid accountId, CancellationToken cancellationToken = default)
        {
            GetAccountRoleNameCallCount++;
            return Task.FromResult(_roleName);
        }

        public Task<int> CountSubmittedInYearAsync(int year, CancellationToken cancellationToken = default)
        {
            CountSubmittedInYearCallCount++;
            return Task.FromResult(_submittedCount);
        }

        public Task<ProjectDetailReadModel?> GetDetailAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            GetDetailCallCount++;
            return Task.FromResult(_detail?.ProjectId == projectId ? _detail : null);
        }

        public Task<IReadOnlyList<ProjectListItemReadModel>> GetListAsync(
            ProjectListQueryReadModel query,
            CancellationToken cancellationToken = default)
        {
            GetListCallCount++;
            LastListQuery = query;
            return Task.FromResult(_listItems);
        }

        public Task<int> CountAsync(
            ProjectListQueryReadModel query,
            CancellationToken cancellationToken = default)
        {
            CountCallCount++;
            LastListQuery = query;
            return Task.FromResult(_listItems.Count);
        }

        public IQueryable<Project> Query() => _projects.AsQueryable();
        public Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            GetByIdCallCount++;
            return Task.FromResult(_projects.FirstOrDefault(project => project.ProjectId == id));
        }

        public Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Project>>(_projects);
        }

        public Task AddAsync(Project entity, CancellationToken cancellationToken = default)
        {
            AddCallCount++;
            _projects.Add(entity);
            return Task.CompletedTask;
        }

        public Task AddRangeAsync(IEnumerable<Project> entities, CancellationToken cancellationToken = default)
        {
            _projects.AddRange(entities);
            return Task.CompletedTask;
        }

        public void Update(Project entity)
        {
        }

        public void Remove(Project entity)
        {
            _projects.Remove(entity);
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCallCount++;
            return Task.FromResult(1);
        }
    }
}
