#nullable enable

using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers.Projects;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.ProjectChatMessages;
using FurniSpace.Application.DTOs.Projects;
using FurniSpace.Application.DTOs.Proposals;
using FurniSpace.Application.Interfaces.ProjectChatMessages;
using FurniSpace.Application.Interfaces.MeasurementImages;
using FurniSpace.Application.Interfaces.Projects;
using FurniSpace.Application.Interfaces.Proposals;
using FurniSpace.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurniSpace.API.Tests.Controllers;

public sealed class ProjectsControllerTests
{
    private static readonly IMeasurementImageService NoOpMeasurementImages = new NoOpMeasurementImageService();
    [Fact]
    public void Controller_RequiresAuthorization()
    {
        var authorize = typeof(ProjectsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .SingleOrDefault();

        Assert.NotNull(authorize);
        Assert.Null(authorize.Roles);
    }

    [Fact]
    public void Create_RequiresCustomerRole()
    {
        var authorize = GetMethodAuthorizeAttribute(nameof(ProjectsController.Create));

        Assert.NotNull(authorize);
        Assert.Equal("CUSTOMER", authorize.Roles);
    }

    [Fact]
    public void GetList_AllowsAllProjectRoles()
    {
        var authorize = GetMethodAuthorizeAttribute(nameof(ProjectsController.GetList));

        Assert.NotNull(authorize);
        Assert.Equal("SALES,ADMIN,CUSTOMER,DESIGNER", authorize.Roles);
    }

    [Fact]
    public void GetById_AllowsProjectParticipantRoles()
    {
        var authorize = GetMethodAuthorizeAttribute(nameof(ProjectsController.GetById));

        Assert.NotNull(authorize);
        Assert.Equal("SALES,ADMIN,CUSTOMER,DESIGNER", authorize.Roles);
    }

    [Fact]
    public void GetPublishedProposal_RequiresCustomerRole()
    {
        var authorize = GetMethodAuthorizeAttribute(nameof(ProjectsController.GetPublishedProposal));

        Assert.NotNull(authorize);
        Assert.Equal("CUSTOMER", authorize.Roles);
    }

    [Fact]
    public void GetByUser_AllowsAllProjectRoles()
    {
        var authorize = GetMethodAuthorizeAttribute(nameof(ProjectsController.GetByUser));

        Assert.NotNull(authorize);
        Assert.Equal("ADMIN,SALES,DESIGNER,CUSTOMER", authorize.Roles);
    }

    [Fact]
    public void AssignSales_AllowsSalesAndAdminRoles()
    {
        var authorize = GetMethodAuthorizeAttribute(nameof(ProjectsController.AssignSales));

        Assert.NotNull(authorize);
        Assert.Equal("SALES,ADMIN", authorize.Roles);
    }

    [Fact]
    public void RequestInformation_AllowsSalesAndAdminRoles()
    {
        var authorize = GetMethodAuthorizeAttribute(nameof(ProjectsController.RequestInformation));

        Assert.NotNull(authorize);
        Assert.Equal("SALES,ADMIN", authorize.Roles);
    }

    [Fact]
    public void UpdateBasicInformation_AllowsCustomerSalesAndAdminRoles()
    {
        var authorize = GetMethodAuthorizeAttribute(nameof(ProjectsController.UpdateBasicInformation));

        Assert.NotNull(authorize);
        Assert.Equal("CUSTOMER,SALES,ADMIN", authorize.Roles);
    }

    [Fact]
    public void UpdateTargetCompletionDate_AllowsCustomerSalesAndAdminRoles()
    {
        var authorize = GetMethodAuthorizeAttribute(nameof(ProjectsController.UpdateTargetCompletionDate));

        Assert.NotNull(authorize);
        Assert.Equal("CUSTOMER,SALES,ADMIN", authorize.Roles);
    }

    [Fact]
    public void UpdateStatus_AllowsSalesDesignerAndAdminRoles()
    {
        var authorize = GetMethodAuthorizeAttribute(nameof(ProjectsController.UpdateStatus));

        Assert.NotNull(authorize);
        Assert.Equal("SALES,DESIGNER,ADMIN", authorize.Roles);
    }

    [Fact]
    public void Complete_AllowsSalesAndAdminRoles()
    {
        var authorize = GetMethodAuthorizeAttribute(nameof(ProjectsController.Complete));

        Assert.NotNull(authorize);
        Assert.Equal("SALES,ADMIN", authorize.Roles);
    }

    [Fact]
    public void Reject_AllowsSalesAndAdminRoles()
    {
        var authorize = GetMethodAuthorizeAttribute(nameof(ProjectsController.Reject));

        Assert.NotNull(authorize);
        Assert.Equal("SALES,ADMIN", authorize.Roles);
    }

    [Fact]
    public void ReopenProposal_AllowsCustomerSalesAndAdminRoles()
    {
        var authorize = GetMethodAuthorizeAttribute(nameof(ProjectsController.ReopenProposal));

        Assert.NotNull(authorize);
        Assert.Equal("CUSTOMER,SALES,ADMIN", authorize.Roles);
    }

    [Fact]
    public void AssignDesigner_AllowsSalesAndAdminRoles()
    {
        var authorize = GetMethodAuthorizeAttribute(nameof(ProjectsController.AssignDesigner));

        Assert.NotNull(authorize);
        Assert.Equal("SALES,ADMIN", authorize.Roles);
    }

    [Fact]
    public async Task Create_ReturnsCreatedServiceResultThroughBaseController()
    {
        var customerId = Guid.NewGuid();
        var response = new ProjectDto
        {
            ProjectId = Guid.NewGuid(),
            CustomerId = customerId,
            ProjectCode = "PRJ-2026-0001",
            ProjectName = "Moc Coffee Interior Setup",
            BusinessType = "Cafe",
            FurnitureRequirement = "Tables",
            Status = ProjectStatus.SUBMITTED,
            SubmittedAt = DateTime.UtcNow
        };
        var service = new FakeProjectService(
            ServiceResult<ProjectDto>.Created(response, "Project request submitted successfully."));
        var controller = new ProjectsController(service, new FakeProjectChatMessageService(), new FakeProposalService(), NoOpMeasurementImages)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, customerId.ToString())
                    ], "TestAuth"))
                }
            }
        };
        var request = new CreateProjectRequestDto
        {
            ProjectName = "Moc Coffee Interior Setup",
            BusinessType = "Cafe",
            FurnitureRequirement = "Tables"
        };

        var actionResult = await controller.Create(request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(201, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<ProjectDto>>(objectResult.Value);
        Assert.Equal(201, result.Status);
        Assert.Equal("Project request submitted successfully.", result.Message);
        Assert.Same(response, result.Data);
        Assert.Equal(customerId, service.CurrentUserId);
        Assert.Same(request, service.Request);
    }

    [Fact]
    public async Task Create_WithoutUserIdClaim_ReturnsUnauthorized()
    {
        var service = new FakeProjectService(ServiceResult<ProjectDto>.Created(new ProjectDto()));
        var controller = new ProjectsController(service, new FakeProjectChatMessageService(), new FakeProposalService(), NoOpMeasurementImages)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var actionResult = await controller.Create(new CreateProjectRequestDto());

        Assert.IsType<UnauthorizedResult>(actionResult);
        Assert.Equal(Guid.Empty, service.CurrentUserId);
        Assert.Null(service.Request);
    }

    [Fact]
    public async Task GetList_ReturnsServiceResultThroughBaseController()
    {
        var currentUserId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var response = new ProjectListResponseDto
        {
            Page = 2,
            Limit = 10,
            Total = 1,
            Items =
            [
                new ProjectListItemDto
                {
                    ProjectId = Guid.NewGuid(),
                    ProjectCode = "PRJ-2026-0001",
                    ProjectName = "Moc Coffee",
                    BusinessType = "Cafe",
                    Status = ProjectStatus.SUBMITTED,
                    CustomerId = Guid.NewGuid(),
                    AssignedSalesId = salesId,
                    AssignedDesignerId = designerId,
                    SubmittedAt = DateTime.UtcNow
                }
            ]
        };
        var service = new FakeProjectService(
            createResult: ServiceResult<ProjectDto>.Created(new ProjectDto()),
            listResult: ServiceResult<ProjectListResponseDto>.Success(
                response,
                "Project request queue retrieved successfully."));
        var controller = CreateControllerWithUser(service, currentUserId);

        var actionResult = await controller.GetList(
            ProjectStatus.SUBMITTED,
            salesId,
            designerId,
            "moc",
            page: 2,
            limit: 10);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<ProjectListResponseDto>>(objectResult.Value);
        Assert.Equal(200, result.Status);
        Assert.Equal("Project request queue retrieved successfully.", result.Message);
        Assert.Same(response, result.Data);
        Assert.Equal(currentUserId, service.CurrentUserId);
        Assert.NotNull(service.Query);
        Assert.Equal(ProjectStatus.SUBMITTED, service.Query.Status);
        Assert.Equal(salesId, service.Query.AssignedSalesId);
        Assert.Equal(designerId, service.Query.AssignedDesignerId);
        Assert.Equal("moc", service.Query.Search);
        Assert.Equal(2, service.Query.Page);
        Assert.Equal(10, service.Query.Limit);
    }

    [Fact]
    public async Task GetList_WithoutUserIdClaim_ReturnsUnauthorized()
    {
        var service = new FakeProjectService(ServiceResult<ProjectDto>.Created(new ProjectDto()));
        var controller = new ProjectsController(service, new FakeProjectChatMessageService(), new FakeProposalService(), NoOpMeasurementImages)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var actionResult = await controller.GetList();

        Assert.IsType<UnauthorizedResult>(actionResult);
        Assert.Equal(Guid.Empty, service.CurrentUserId);
        Assert.Null(service.Query);
    }

    [Fact]
    public async Task GetByUser_ReturnsServiceResultThroughBaseController()
    {
        var currentUserId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var response = new ProjectsByUserResponseDto
        {
            Page = 2,
            PageSize = 10,
            TotalItems = 1,
            TotalPages = 1
        };
        var service = new FakeProjectService(
            createResult: ServiceResult<ProjectDto>.Created(new ProjectDto()),
            projectsByUserResult: ServiceResult<ProjectsByUserResponseDto>.Success(
                response,
                "Projects retrieved successfully."));
        var controller = CreateControllerWithUser(service, currentUserId);

        var actionResult = await controller.GetByUser(
            userId,
            page: 2,
            pageSize: 10,
            status: ProjectStatus.IN_CONSULTATION,
            roleScope: "DESIGNER",
            keyword: "cafe");

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<ProjectsByUserResponseDto>>(objectResult.Value);
        Assert.Equal(200, result.Status);
        Assert.Same(response, result.Data);
        Assert.Equal(userId, service.UserId);
        Assert.Equal(currentUserId, service.CurrentUserId);
        Assert.NotNull(service.ProjectsByUserQuery);
        Assert.Equal(2, service.ProjectsByUserQuery.Page);
        Assert.Equal(10, service.ProjectsByUserQuery.PageSize);
        Assert.Equal(ProjectStatus.IN_CONSULTATION, service.ProjectsByUserQuery.Status);
        Assert.Equal("DESIGNER", service.ProjectsByUserQuery.RoleScope);
        Assert.Equal("cafe", service.ProjectsByUserQuery.Keyword);
    }

    [Fact]
    public async Task GetByUser_WithoutUserIdClaim_ReturnsUnauthorized()
    {
        var service = new FakeProjectService(ServiceResult<ProjectDto>.Created(new ProjectDto()));
        var controller = new ProjectsController(service, new FakeProjectChatMessageService(), new FakeProposalService(), NoOpMeasurementImages)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var actionResult = await controller.GetByUser(Guid.NewGuid());

        Assert.IsType<UnauthorizedResult>(actionResult);
        Assert.Equal(Guid.Empty, service.UserId);
        Assert.Null(service.ProjectsByUserQuery);
    }

    [Fact]
    public async Task GetById_ReturnsServiceResultThroughBaseController()
    {
        var currentUserId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var response = new ProjectDto
        {
            ProjectId = projectId,
            CustomerId = currentUserId,
            ProjectCode = "PRJ-2026-0001",
            ProjectName = "Moc Coffee",
            BusinessType = "Cafe",
            FurnitureRequirement = "Tables",
            Status = ProjectStatus.SUBMITTED,
            SubmittedAt = DateTime.UtcNow
        };
        var service = new FakeProjectService(
            createResult: ServiceResult<ProjectDto>.Created(new ProjectDto()),
            detailResult: ServiceResult<ProjectDto>.Success(
                response,
                "Project detail retrieved successfully."));
        var controller = CreateControllerWithUser(service, currentUserId);

        var actionResult = await controller.GetById(projectId);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<ProjectDto>>(objectResult.Value);
        Assert.Equal(200, result.Status);
        Assert.Equal("Project detail retrieved successfully.", result.Message);
        Assert.Same(response, result.Data);
        Assert.Equal(projectId, service.ProjectId);
        Assert.Equal(currentUserId, service.CurrentUserId);
    }

    [Fact]
    public async Task GetById_WithoutUserIdClaim_ReturnsUnauthorized()
    {
        var service = new FakeProjectService(ServiceResult<ProjectDto>.Created(new ProjectDto()));
        var controller = new ProjectsController(service, new FakeProjectChatMessageService(), new FakeProposalService(), NoOpMeasurementImages)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var actionResult = await controller.GetById(Guid.NewGuid());

        Assert.IsType<UnauthorizedResult>(actionResult);
        Assert.Equal(Guid.Empty, service.ProjectId);
        Assert.Equal(Guid.Empty, service.CurrentUserId);
    }

    [Fact]
    public async Task GetPublishedProposal_ReturnsServiceResultThroughBaseController()
    {
        var currentUserId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var response = new PublishedProposalDto { ProjectId = projectId };
        var proposalService = new FakeProposalService(
            publishedProposalResult: ServiceResult<PublishedProposalDto>.Success(
                response,
                "Published proposal retrieved successfully."));
        var controller = CreateControllerWithUser(
            new FakeProjectService(ServiceResult<ProjectDto>.Created(new ProjectDto())),
            currentUserId,
            proposalService);

        var actionResult = await controller.GetPublishedProposal(projectId);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<PublishedProposalDto>>(objectResult.Value);
        Assert.Equal(200, result.Status);
        Assert.Same(response, result.Data);
        Assert.Equal(projectId, proposalService.ProjectId);
        Assert.Equal(currentUserId, proposalService.CurrentUserId);
    }

    [Fact]
    public async Task GetPublishedProposal_WithoutUserIdClaim_ReturnsUnauthorized()
    {
        var proposalService = new FakeProposalService();
        var controller = new ProjectsController(
            new FakeProjectService(ServiceResult<ProjectDto>.Created(new ProjectDto())),
            new FakeProjectChatMessageService(),
            proposalService,
            NoOpMeasurementImages)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var actionResult = await controller.GetPublishedProposal(Guid.NewGuid());

        Assert.IsType<UnauthorizedResult>(actionResult);
        Assert.Equal(Guid.Empty, proposalService.ProjectId);
        Assert.Equal(Guid.Empty, proposalService.CurrentUserId);
    }

    [Fact]
    public async Task AssignSales_ReturnsServiceResultThroughBaseController()
    {
        var currentUserId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var response = new ProjectSalesAssignmentDto
        {
            ProjectId = projectId,
            AssignedSalesId = currentUserId,
            Status = ProjectStatus.IN_CONSULTATION,
            SalesAssignedAt = DateTime.UtcNow
        };
        var service = new FakeProjectService(
            createResult: ServiceResult<ProjectDto>.Created(new ProjectDto()),
            assignSalesResult: ServiceResult<ProjectSalesAssignmentDto>.Success(
                response,
                "Project request accepted successfully."));
        var controller = CreateControllerWithUser(service, currentUserId);
        var request = new AssignProjectSalesRequestDto
        {
            Note = "Accepted for consultation."
        };

        var actionResult = await controller.AssignSales(projectId, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<ProjectSalesAssignmentDto>>(objectResult.Value);
        Assert.Equal(200, result.Status);
        Assert.Equal("Project request accepted successfully.", result.Message);
        Assert.Same(response, result.Data);
        Assert.Equal(projectId, service.ProjectId);
        Assert.Equal(currentUserId, service.CurrentUserId);
        Assert.Same(request, service.AssignSalesRequest);
    }

    [Fact]
    public async Task AssignSales_WithoutUserIdClaim_ReturnsUnauthorized()
    {
        var service = new FakeProjectService(ServiceResult<ProjectDto>.Created(new ProjectDto()));
        var controller = new ProjectsController(service, new FakeProjectChatMessageService(), new FakeProposalService(), NoOpMeasurementImages)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var actionResult = await controller.AssignSales(Guid.NewGuid(), new AssignProjectSalesRequestDto());

        Assert.IsType<UnauthorizedResult>(actionResult);
        Assert.Equal(Guid.Empty, service.ProjectId);
        Assert.Equal(Guid.Empty, service.CurrentUserId);
        Assert.Null(service.AssignSalesRequest);
    }

    [Fact]
    public async Task RequestInformation_ReturnsServiceResultThroughBaseController()
    {
        var currentUserId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var response = new ProjectInformationRequestDto
        {
            ProjectId = projectId,
            Status = ProjectStatus.NEED_BASIC_INFORMATION,
            RequestedAt = DateTime.UtcNow
        };
        var service = new FakeProjectService(
            createResult: ServiceResult<ProjectDto>.Created(new ProjectDto()),
            requestInformationResult: ServiceResult<ProjectInformationRequestDto>.Success(
                response,
                "More information requested successfully."));
        var controller = CreateControllerWithUser(service, currentUserId);
        var request = new RequestProjectInformationRequestDto
        {
            Message = "Please provide exact store dimensions."
        };

        var actionResult = await controller.RequestInformation(projectId, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<ProjectInformationRequestDto>>(objectResult.Value);
        Assert.Equal(200, result.Status);
        Assert.Equal("More information requested successfully.", result.Message);
        Assert.Same(response, result.Data);
        Assert.Equal(projectId, service.ProjectId);
        Assert.Equal(currentUserId, service.CurrentUserId);
        Assert.Same(request, service.RequestInformationRequest);
    }

    [Fact]
    public async Task RequestInformation_WithoutUserIdClaim_ReturnsUnauthorized()
    {
        var service = new FakeProjectService(ServiceResult<ProjectDto>.Created(new ProjectDto()));
        var controller = new ProjectsController(service, new FakeProjectChatMessageService(), new FakeProposalService(), NoOpMeasurementImages)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var actionResult = await controller.RequestInformation(Guid.NewGuid(), new RequestProjectInformationRequestDto());

        Assert.IsType<UnauthorizedResult>(actionResult);
        Assert.Equal(Guid.Empty, service.ProjectId);
        Assert.Equal(Guid.Empty, service.CurrentUserId);
        Assert.Null(service.RequestInformationRequest);
    }

    [Fact]
    public async Task UpdateBasicInformation_ReturnsServiceResultThroughBaseController()
    {
        var currentUserId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var response = new ProjectBasicInformationDto
        {
            ProjectId = projectId,
            ProjectName = "Moc Coffee Interior Setup",
            Status = ProjectStatus.IN_CONSULTATION,
            UpdatedAt = DateTime.UtcNow
        };
        var service = new FakeProjectService(
            createResult: ServiceResult<ProjectDto>.Created(new ProjectDto()),
            updateBasicInformationResult: ServiceResult<ProjectBasicInformationDto>.Success(
                response,
                "Project basic information updated successfully."));
        var controller = CreateControllerWithUser(service, currentUserId);
        var request = new UpdateProjectBasicInformationRequestDto
        {
            ProjectName = "Moc Coffee Interior Setup",
            BusinessType = "Cafe",
            FurnitureRequirement = "Tables"
        };

        var actionResult = await controller.UpdateBasicInformation(projectId, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<ProjectBasicInformationDto>>(objectResult.Value);
        Assert.Equal(200, result.Status);
        Assert.Equal("Project basic information updated successfully.", result.Message);
        Assert.Same(response, result.Data);
        Assert.Equal(projectId, service.ProjectId);
        Assert.Equal(currentUserId, service.CurrentUserId);
        Assert.Same(request, service.UpdateBasicInformationRequest);
    }

    [Fact]
    public async Task UpdateTargetCompletionDate_ReturnsServiceResultThroughBaseController()
    {
        var currentUserId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var targetDate = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(45);
        var response = new ProjectTargetCompletionDateDto
        {
            ProjectId = projectId,
            TargetCompletionDate = targetDate,
            UpdatedAt = DateTime.UtcNow
        };
        var service = new FakeProjectService(
            createResult: ServiceResult<ProjectDto>.Created(new ProjectDto()),
            updateTargetCompletionDateResult: ServiceResult<ProjectTargetCompletionDateDto>.Success(
                response,
                "Project target completion date updated successfully."));
        var controller = CreateControllerWithUser(service, currentUserId);
        var request = new UpdateProjectTargetCompletionDateRequestDto
        {
            TargetCompletionDate = targetDate
        };

        var actionResult = await controller.UpdateTargetCompletionDate(projectId, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<ProjectTargetCompletionDateDto>>(objectResult.Value);
        Assert.Equal(200, result.Status);
        Assert.Equal("Project target completion date updated successfully.", result.Message);
        Assert.Same(response, result.Data);
        Assert.Equal(projectId, service.ProjectId);
        Assert.Equal(currentUserId, service.CurrentUserId);
        Assert.Same(request, service.UpdateTargetCompletionDateRequest);
    }

    [Fact]
    public async Task UpdateTargetCompletionDate_WithoutUserIdClaim_ReturnsUnauthorized()
    {
        var service = new FakeProjectService(ServiceResult<ProjectDto>.Created(new ProjectDto()));
        var controller = new ProjectsController(service, new FakeProjectChatMessageService(), new FakeProposalService(), NoOpMeasurementImages)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var actionResult = await controller.UpdateTargetCompletionDate(
            Guid.NewGuid(),
            new UpdateProjectTargetCompletionDateRequestDto());

        Assert.IsType<UnauthorizedResult>(actionResult);
        Assert.Equal(Guid.Empty, service.ProjectId);
        Assert.Equal(Guid.Empty, service.CurrentUserId);
        Assert.Null(service.UpdateTargetCompletionDateRequest);
    }

    [Fact]
    public async Task UpdateBasicInformation_WithoutUserIdClaim_ReturnsUnauthorized()
    {
        var service = new FakeProjectService(ServiceResult<ProjectDto>.Created(new ProjectDto()));
        var controller = new ProjectsController(service, new FakeProjectChatMessageService(), new FakeProposalService(), NoOpMeasurementImages)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var actionResult = await controller.UpdateBasicInformation(Guid.NewGuid(), new UpdateProjectBasicInformationRequestDto());

        Assert.IsType<UnauthorizedResult>(actionResult);
        Assert.Equal(Guid.Empty, service.ProjectId);
        Assert.Equal(Guid.Empty, service.CurrentUserId);
        Assert.Null(service.UpdateBasicInformationRequest);
    }

    [Fact]
    public async Task UpdateStatus_ReturnsServiceResultThroughBaseController()
    {
        var currentUserId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var response = new ProjectStatusUpdateDto
        {
            ProjectId = projectId,
            Status = ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT,
            UpdatedAt = DateTime.UtcNow
        };
        var service = new FakeProjectService(
            createResult: ServiceResult<ProjectDto>.Created(new ProjectDto()),
            updateStatusResult: ServiceResult<ProjectStatusUpdateDto>.Success(
                response,
                "Project status updated successfully."));
        var controller = CreateControllerWithUser(service, currentUserId);
        var request = new UpdateProjectStatusRequestDto
        {
            Status = ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT,
            Note = "Ready."
        };

        var actionResult = await controller.UpdateStatus(projectId, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<ProjectStatusUpdateDto>>(objectResult.Value);
        Assert.Equal(200, result.Status);
        Assert.Equal("Project status updated successfully.", result.Message);
        Assert.Same(response, result.Data);
        Assert.Equal(projectId, service.ProjectId);
        Assert.Equal(currentUserId, service.CurrentUserId);
        Assert.Same(request, service.UpdateStatusRequest);
    }

    [Fact]
    public async Task UpdateStatus_WithoutUserIdClaim_ReturnsUnauthorized()
    {
        var service = new FakeProjectService(ServiceResult<ProjectDto>.Created(new ProjectDto()));
        var controller = new ProjectsController(service, new FakeProjectChatMessageService(), new FakeProposalService(), NoOpMeasurementImages)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var actionResult = await controller.UpdateStatus(Guid.NewGuid(), new UpdateProjectStatusRequestDto());

        Assert.IsType<UnauthorizedResult>(actionResult);
        Assert.Equal(Guid.Empty, service.ProjectId);
        Assert.Equal(Guid.Empty, service.CurrentUserId);
        Assert.Null(service.UpdateStatusRequest);
    }

    [Fact]
    public async Task Complete_ReturnsServiceResultThroughBaseController()
    {
        var currentUserId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var response = new ProjectCompletionDto
        {
            ProjectId = projectId,
            ProjectStatus = nameof(ProjectStatus.COMPLETED),
            CompletedAt = DateTime.UtcNow
        };
        var service = new FakeProjectService(
            createResult: ServiceResult<ProjectDto>.Created(new ProjectDto()),
            completeResult: ServiceResult<ProjectCompletionDto>.Success(
                response,
                "Project completed successfully."));
        var controller = CreateControllerWithUser(service, currentUserId);

        var actionResult = await controller.Complete(projectId);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<ProjectCompletionDto>>(objectResult.Value);
        Assert.Equal(200, result.Status);
        Assert.Equal("Project completed successfully.", result.Message);
        Assert.Same(response, result.Data);
        Assert.Equal(projectId, service.ProjectId);
        Assert.Equal(currentUserId, service.CurrentUserId);
    }

    [Fact]
    public async Task Complete_WithoutUserIdClaim_ReturnsUnauthorized()
    {
        var service = new FakeProjectService(ServiceResult<ProjectDto>.Created(new ProjectDto()));
        var controller = new ProjectsController(service, new FakeProjectChatMessageService(), new FakeProposalService(), NoOpMeasurementImages)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var actionResult = await controller.Complete(Guid.NewGuid());

        Assert.IsType<UnauthorizedResult>(actionResult);
        Assert.Equal(Guid.Empty, service.ProjectId);
        Assert.Equal(Guid.Empty, service.CurrentUserId);
    }

    [Fact]
    public async Task Reject_ReturnsServiceResultThroughBaseController()
    {
        var currentUserId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var response = new ProjectRejectionDto
        {
            ProjectId = projectId,
            Status = ProjectStatus.REJECTED,
            RejectionReason = "Unsupported scope.",
            RejectedAt = DateTime.UtcNow
        };
        var service = new FakeProjectService(
            createResult: ServiceResult<ProjectDto>.Created(new ProjectDto()),
            rejectResult: ServiceResult<ProjectRejectionDto>.Success(
                response,
                "Project request rejected."));
        var controller = CreateControllerWithUser(service, currentUserId);
        var request = new RejectProjectRequestDto
        {
            RejectionReason = "Unsupported scope."
        };

        var actionResult = await controller.Reject(projectId, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<ProjectRejectionDto>>(objectResult.Value);
        Assert.Equal(200, result.Status);
        Assert.Equal("Project request rejected.", result.Message);
        Assert.Same(response, result.Data);
        Assert.Equal(projectId, service.ProjectId);
        Assert.Equal(currentUserId, service.CurrentUserId);
        Assert.Same(request, service.RejectRequest);
    }

    [Fact]
    public async Task Reject_WithoutUserIdClaim_ReturnsUnauthorized()
    {
        var service = new FakeProjectService(ServiceResult<ProjectDto>.Created(new ProjectDto()));
        var controller = new ProjectsController(service, new FakeProjectChatMessageService(), new FakeProposalService(), NoOpMeasurementImages)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var actionResult = await controller.Reject(Guid.NewGuid(), new RejectProjectRequestDto());

        Assert.IsType<UnauthorizedResult>(actionResult);
        Assert.Equal(Guid.Empty, service.ProjectId);
        Assert.Equal(Guid.Empty, service.CurrentUserId);
        Assert.Null(service.RejectRequest);
    }

    [Fact]
    public async Task AssignDesigner_ReturnsServiceResultThroughBaseController()
    {
        var currentUserId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var response = new ProjectDesignerAssignmentDto
        {
            ProjectId = projectId,
            AssignedDesigner = new AssignedDesignerDto
            {
                AccountId = designerId,
                FullName = "Le Designer"
            },
            Status = ProjectStatus.SPACE_VERIFIED,
            DesignerAssignedAt = DateTime.UtcNow
        };
        var service = new FakeProjectService(
            createResult: ServiceResult<ProjectDto>.Created(new ProjectDto()),
            assignDesignerResult: ServiceResult<ProjectDesignerAssignmentDto>.Success(
                response,
                "Designer assigned successfully."));
        var controller = CreateControllerWithUser(service, currentUserId);
        var request = new AssignProjectDesignerRequestDto
        {
            DesignerId = designerId,
            SpaceDataStatus = ProjectSpaceDataStatus.SUFFICIENT,
            Note = "Please review."
        };

        var actionResult = await controller.AssignDesigner(projectId, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<ProjectDesignerAssignmentDto>>(objectResult.Value);
        Assert.Equal(200, result.Status);
        Assert.Equal("Designer assigned successfully.", result.Message);
        Assert.Same(response, result.Data);
        Assert.Equal(projectId, service.ProjectId);
        Assert.Equal(currentUserId, service.CurrentUserId);
        Assert.Same(request, service.AssignDesignerRequest);
    }

    [Fact]
    public async Task AssignDesigner_WithoutUserIdClaim_ReturnsUnauthorized()
    {
        var service = new FakeProjectService(ServiceResult<ProjectDto>.Created(new ProjectDto()));
        var controller = new ProjectsController(service, new FakeProjectChatMessageService(), new FakeProposalService(), NoOpMeasurementImages)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var actionResult = await controller.AssignDesigner(Guid.NewGuid(), new AssignProjectDesignerRequestDto());

        Assert.IsType<UnauthorizedResult>(actionResult);
        Assert.Equal(Guid.Empty, service.ProjectId);
        Assert.Equal(Guid.Empty, service.CurrentUserId);
        Assert.Null(service.AssignDesignerRequest);
    }

    [Fact]
    public void SearchChatMessages_AllowsProjectParticipantRoles()
    {
        var authorize = GetMethodAuthorizeAttribute(nameof(ProjectsController.SearchChatMessages));

        Assert.NotNull(authorize);
        Assert.Equal("SALES,ADMIN,CUSTOMER,DESIGNER", authorize.Roles);
    }

    [Fact]
    public void GetMeasurementImages_AllowsProjectParticipantRoles()
    {
        var authorize = GetMethodAuthorizeAttribute(nameof(ProjectsController.GetMeasurementImages));

        Assert.NotNull(authorize);
        Assert.Equal("CUSTOMER,SALES,DESIGNER,ADMIN", authorize.Roles);
    }

    [Fact]
    public async Task SearchChatMessages_ReturnsServiceResultThroughBaseController()
    {
        var currentUserId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var response = new ProjectChatMessageSearchResponseDto { Page = 1, Limit = 20, Total = 0, Items = [] };
        var chatService = new FakeProjectChatMessageService(
            searchResult: ServiceResult<ProjectChatMessageSearchResponseDto>.Success(response, "Search completed."));
        var controller = CreateControllerWithUser(
            new FakeProjectService(ServiceResult<ProjectDto>.Created(new ProjectDto())),
            currentUserId,
            chatService: chatService);

        var actionResult = await controller.SearchChatMessages(projectId, "hello", page: 2, limit: 10);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(projectId, chatService.ProjectId);
        Assert.Equal(currentUserId, chatService.CurrentUserId);
        Assert.Equal("hello", chatService.SearchQuery);
        Assert.Equal(2, chatService.SearchPage);
        Assert.Equal(10, chatService.SearchLimit);
    }

    [Fact]
    public async Task SearchChatMessages_WithoutUserIdClaim_ReturnsUnauthorized()
    {
        var controller = new ProjectsController(
            new FakeProjectService(ServiceResult<ProjectDto>.Created(new ProjectDto())),
            new FakeProjectChatMessageService(),
            new FakeProposalService(),
            NoOpMeasurementImages)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var actionResult = await controller.SearchChatMessages(Guid.NewGuid(), "hello");

        Assert.IsType<UnauthorizedResult>(actionResult);
    }

    [Fact]
    public async Task GetMeasurementImages_ReturnsServiceResultThroughBaseController()
    {
        var currentUserId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        var areaId = Guid.NewGuid();
        var response = new FurniSpace.Application.DTOs.MeasurementImages.MeasurementImageGalleryResponseDto
        {
            Page = 1,
            Limit = 20,
            Total = 0,
            Items = []
        };
        var measurementService = new RecordingMeasurementImageService(
            ServiceResult<FurniSpace.Application.DTOs.MeasurementImages.MeasurementImageGalleryResponseDto>.Success(
                response,
                "Gallery retrieved."));
        var controller = CreateControllerWithUser(
            new FakeProjectService(ServiceResult<ProjectDto>.Created(new ProjectDto())),
            currentUserId,
            measurementImages: measurementService);

        var actionResult = await controller.GetMeasurementImages(
            projectId,
            scheduleId,
            areaId,
            assigned: true,
            page: 2,
            limit: 10);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(projectId, measurementService.ProjectId);
        Assert.Equal(currentUserId, measurementService.CurrentUserId);
        Assert.NotNull(measurementService.LastQuery);
        Assert.Equal(scheduleId, measurementService.LastQuery.ScheduleId);
        Assert.Equal(areaId, measurementService.LastQuery.ProjectAreaId);
        Assert.True(measurementService.LastQuery.Assigned);
        Assert.Equal(2, measurementService.LastQuery.Page);
        Assert.Equal(10, measurementService.LastQuery.Limit);
    }

    [Fact]
    public async Task GetMeasurementImages_WithoutUserIdClaim_ReturnsUnauthorized()
    {
        var controller = new ProjectsController(
            new FakeProjectService(ServiceResult<ProjectDto>.Created(new ProjectDto())),
            new FakeProjectChatMessageService(),
            new FakeProposalService(),
            NoOpMeasurementImages)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var actionResult = await controller.GetMeasurementImages(Guid.NewGuid());

        Assert.IsType<UnauthorizedResult>(actionResult);
    }

    [Fact]
    public async Task ReopenProposal_ReturnsServiceResultThroughBaseController()
    {
        var currentUserId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var response = new ReopenProposalResponseDto { ProjectId = projectId };
        var service = new FakeProjectService(
            createResult: ServiceResult<ProjectDto>.Created(new ProjectDto()),
            reopenProposalResult: ServiceResult<ReopenProposalResponseDto>.Success(response, "Proposal reopened."));
        var controller = CreateControllerWithUser(service, currentUserId);

        var actionResult = await controller.ReopenProposal(projectId);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(projectId, service.ProjectId);
        Assert.Equal(currentUserId, service.CurrentUserId);
    }

    [Fact]
    public async Task ReopenProposal_WithoutUserIdClaim_ReturnsUnauthorized()
    {
        var controller = new ProjectsController(
            new FakeProjectService(ServiceResult<ProjectDto>.Created(new ProjectDto())),
            new FakeProjectChatMessageService(),
            new FakeProposalService(),
            NoOpMeasurementImages)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var actionResult = await controller.ReopenProposal(Guid.NewGuid());

        Assert.IsType<UnauthorizedResult>(actionResult);
    }

    private static AuthorizeAttribute? GetMethodAuthorizeAttribute(string methodName)
    {
        return typeof(ProjectsController)
            .GetMethods()
            .Single(method => method.Name == methodName)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .SingleOrDefault();
    }

    private static ProjectsController CreateControllerWithUser(
        FakeProjectService service,
        Guid currentUserId,
        FakeProposalService? proposalService = null,
        FakeProjectChatMessageService? chatService = null,
        IMeasurementImageService? measurementImages = null)
    {
        return new ProjectsController(
            service,
            chatService ?? new FakeProjectChatMessageService(),
            proposalService ?? new FakeProposalService(),
            measurementImages ?? NoOpMeasurementImages)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, currentUserId.ToString())
                    ], "TestAuth"))
                }
            }
        };
    }

    private sealed class FakeProjectService : IProjectService
    {
        private readonly ServiceResult<ProjectDto> _createResult;
        private readonly ServiceResult<ProjectDto> _detailResult;
        private readonly ServiceResult<ProjectListResponseDto> _listResult;
        private readonly ServiceResult<ProjectSalesAssignmentDto> _assignSalesResult;
        private readonly ServiceResult<ProjectInformationRequestDto> _requestInformationResult;
        private readonly ServiceResult<ProjectBasicInformationDto> _updateBasicInformationResult;
        private readonly ServiceResult<ProjectTargetCompletionDateDto> _updateTargetCompletionDateResult;
        private readonly ServiceResult<ProjectStatusUpdateDto> _updateStatusResult;
        private readonly ServiceResult<ProjectRejectionDto> _rejectResult;
        private readonly ServiceResult<ProjectCompletionDto> _completeResult;
        private readonly ServiceResult<ReopenProposalResponseDto> _reopenProposalResult;
        private readonly ServiceResult<ProjectDesignerAssignmentDto> _assignDesignerResult;
        private readonly ServiceResult<ProjectsByUserResponseDto> _projectsByUserResult;

        public FakeProjectService(
            ServiceResult<ProjectDto> createResult,
            ServiceResult<ProjectListResponseDto>? listResult = null,
            ServiceResult<ProjectDto>? detailResult = null,
            ServiceResult<ProjectSalesAssignmentDto>? assignSalesResult = null,
            ServiceResult<ProjectInformationRequestDto>? requestInformationResult = null,
            ServiceResult<ProjectBasicInformationDto>? updateBasicInformationResult = null,
            ServiceResult<ProjectTargetCompletionDateDto>? updateTargetCompletionDateResult = null,
            ServiceResult<ProjectStatusUpdateDto>? updateStatusResult = null,
            ServiceResult<ProjectRejectionDto>? rejectResult = null,
            ServiceResult<ProjectCompletionDto>? completeResult = null,
            ServiceResult<ReopenProposalResponseDto>? reopenProposalResult = null,
            ServiceResult<ProjectDesignerAssignmentDto>? assignDesignerResult = null,
            ServiceResult<ProjectsByUserResponseDto>? projectsByUserResult = null)
        {
            _createResult = createResult;
            _listResult = listResult ?? ServiceResult<ProjectListResponseDto>.Success(new ProjectListResponseDto());
            _detailResult = detailResult ?? ServiceResult<ProjectDto>.Success(new ProjectDto());
            _assignSalesResult = assignSalesResult ?? ServiceResult<ProjectSalesAssignmentDto>.Success(new ProjectSalesAssignmentDto());
            _requestInformationResult = requestInformationResult ??
                ServiceResult<ProjectInformationRequestDto>.Success(new ProjectInformationRequestDto());
            _updateBasicInformationResult = updateBasicInformationResult ??
                ServiceResult<ProjectBasicInformationDto>.Success(new ProjectBasicInformationDto());
            _updateTargetCompletionDateResult = updateTargetCompletionDateResult ??
                ServiceResult<ProjectTargetCompletionDateDto>.Success(new ProjectTargetCompletionDateDto());
            _updateStatusResult = updateStatusResult ??
                ServiceResult<ProjectStatusUpdateDto>.Success(new ProjectStatusUpdateDto());
            _rejectResult = rejectResult ??
                ServiceResult<ProjectRejectionDto>.Success(new ProjectRejectionDto());
            _completeResult = completeResult ??
                ServiceResult<ProjectCompletionDto>.Success(new ProjectCompletionDto());
            _reopenProposalResult = reopenProposalResult ??
                ServiceResult<ReopenProposalResponseDto>.Success(new ReopenProposalResponseDto());
            _assignDesignerResult = assignDesignerResult ??
                ServiceResult<ProjectDesignerAssignmentDto>.Success(new ProjectDesignerAssignmentDto());
            _projectsByUserResult = projectsByUserResult ??
                ServiceResult<ProjectsByUserResponseDto>.Success(new ProjectsByUserResponseDto());
        }

        public Guid CurrentUserId { get; private set; }
        public Guid ProjectId { get; private set; }
        public Guid UserId { get; private set; }
        public CreateProjectRequestDto? Request { get; private set; }
        public AssignProjectSalesRequestDto? AssignSalesRequest { get; private set; }
        public RequestProjectInformationRequestDto? RequestInformationRequest { get; private set; }
        public UpdateProjectBasicInformationRequestDto? UpdateBasicInformationRequest { get; private set; }
        public UpdateProjectTargetCompletionDateRequestDto? UpdateTargetCompletionDateRequest { get; private set; }
        public UpdateProjectStatusRequestDto? UpdateStatusRequest { get; private set; }
        public RejectProjectRequestDto? RejectRequest { get; private set; }
        public AssignProjectDesignerRequestDto? AssignDesignerRequest { get; private set; }
        public ProjectListQueryDto? Query { get; private set; }
        public GetProjectsByUserQueryDto? ProjectsByUserQuery { get; private set; }

        public Task<ServiceResult<ProjectDto>> CreateAsync(
            Guid currentUserId,
            CreateProjectRequestDto request,
            CancellationToken cancellationToken = default)
        {
            CurrentUserId = currentUserId;
            Request = request;
            return Task.FromResult(_createResult);
        }

        public Task<ServiceResult<ProjectDto>> GetByIdAsync(
            Guid projectId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            ProjectId = projectId;
            CurrentUserId = currentUserId;
            return Task.FromResult(_detailResult);
        }

        public Task<ServiceResult<ProjectSalesAssignmentDto>> AssignSalesAsync(
            Guid projectId,
            Guid currentUserId,
            AssignProjectSalesRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ProjectId = projectId;
            CurrentUserId = currentUserId;
            AssignSalesRequest = request;
            return Task.FromResult(_assignSalesResult);
        }

        public Task<ServiceResult<ProjectInformationRequestDto>> RequestInformationAsync(
            Guid projectId,
            Guid currentUserId,
            RequestProjectInformationRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ProjectId = projectId;
            CurrentUserId = currentUserId;
            RequestInformationRequest = request;
            return Task.FromResult(_requestInformationResult);
        }

        public Task<ServiceResult<ProjectBasicInformationDto>> UpdateBasicInformationAsync(
            Guid projectId,
            Guid currentUserId,
            UpdateProjectBasicInformationRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ProjectId = projectId;
            CurrentUserId = currentUserId;
            UpdateBasicInformationRequest = request;
            return Task.FromResult(_updateBasicInformationResult);
        }

        public Task<ServiceResult<ProjectTargetCompletionDateDto>> UpdateTargetCompletionDateAsync(
            Guid projectId,
            Guid currentUserId,
            UpdateProjectTargetCompletionDateRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ProjectId = projectId;
            CurrentUserId = currentUserId;
            UpdateTargetCompletionDateRequest = request;
            return Task.FromResult(_updateTargetCompletionDateResult);
        }

        public Task<ServiceResult<ProjectStatusUpdateDto>> UpdateStatusAsync(
            Guid projectId,
            Guid currentUserId,
            UpdateProjectStatusRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ProjectId = projectId;
            CurrentUserId = currentUserId;
            UpdateStatusRequest = request;
            return Task.FromResult(_updateStatusResult);
        }

        public Task<ServiceResult<ProjectRejectionDto>> RejectAsync(
            Guid projectId,
            Guid currentUserId,
            RejectProjectRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ProjectId = projectId;
            CurrentUserId = currentUserId;
            RejectRequest = request;
            return Task.FromResult(_rejectResult);
        }

        public Task<ServiceResult<ProjectCompletionDto>> CompleteAsync(
            Guid projectId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            ProjectId = projectId;
            CurrentUserId = currentUserId;
            return Task.FromResult(_completeResult);
        }

        public Task<ServiceResult<ProjectDesignerAssignmentDto>> AssignDesignerAsync(
            Guid projectId,
            Guid currentUserId,
            AssignProjectDesignerRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ProjectId = projectId;
            CurrentUserId = currentUserId;
            AssignDesignerRequest = request;
            return Task.FromResult(_assignDesignerResult);
        }

        public Task<ServiceResult<ProjectListResponseDto>> GetListAsync(
            Guid currentUserId,
            ProjectListQueryDto query,
            CancellationToken cancellationToken = default)
        {
            CurrentUserId = currentUserId;
            Query = query;
            return Task.FromResult(_listResult);
        }

        public Task<ServiceResult<ProjectsByUserResponseDto>> GetByUserAsync(
            Guid userId,
            Guid currentUserId,
            GetProjectsByUserQueryDto query,
            CancellationToken cancellationToken = default)
        {
            CurrentUserId = currentUserId;
            UserId = userId;
            ProjectsByUserQuery = query;
            return Task.FromResult(_projectsByUserResult);
        }

        public Task<ServiceResult<ReopenProposalResponseDto>> ReopenProposalAsync(
            Guid projectId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            ProjectId = projectId;
            CurrentUserId = currentUserId;
            return Task.FromResult(_reopenProposalResult);
        }
    }

    private sealed class FakeProposalService : IProposalService
    {
        private readonly ServiceResult<PublishedProposalDto> _publishedProposalResult;

        public FakeProposalService(ServiceResult<PublishedProposalDto>? publishedProposalResult = null)
        {
            _publishedProposalResult = publishedProposalResult ??
                ServiceResult<PublishedProposalDto>.Success(new PublishedProposalDto());
        }

        public Guid ProjectId { get; private set; }
        public Guid CurrentUserId { get; private set; }

        public Task<ServiceResult<ProposalDto>> CreateAsync(
            Guid projectId,
            Guid currentUserId,
            CreateProposalRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<ProposalDto>.Created(new ProposalDto()));

        public Task<ServiceResult<ProposalListResponseDto>> GetListByProjectAsync(
            Guid projectId,
            Guid currentUserId,
            ProposalListQueryDto query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<ProposalListResponseDto>.Success(new ProposalListResponseDto()));

        public Task<ServiceResult<ProposalSceneDto>> CreateSceneAsync(
            Guid proposalId,
            Guid currentUserId,
            CreateProposalSceneRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<ProposalSceneDto>.Created(new ProposalSceneDto()));

        public Task<ServiceResult<ProposalSceneListResponseDto>> GetScenesAsync(
            Guid proposalId,
            Guid currentUserId,
            ProposalSceneListQueryDto query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<ProposalSceneListResponseDto>.Success(new ProposalSceneListResponseDto()));

        public Task<ServiceResult<ProposalSceneDetailDto>> GetSceneDetailAsync(
            Guid sceneId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<ProposalSceneDetailDto>.Success(new ProposalSceneDetailDto()));

        public Task<ServiceResult<ProposalDetailDto>> GetDetailAsync(
            Guid proposalId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<ProposalDetailDto>.Success(new ProposalDetailDto()));

        public Task<ServiceResult<PublishedProposalDto>> GetPublishedByProjectAsync(
            Guid projectId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            ProjectId = projectId;
            CurrentUserId = currentUserId;
            return Task.FromResult(_publishedProposalResult);
        }

        public Task<ServiceResult<ProposalItemListResponseDto>> GetItemsAsync(
            Guid proposalId,
            Guid currentUserId,
            ProposalItemListQueryDto query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<ProposalItemListResponseDto>.Success(new ProposalItemListResponseDto()));

        public Task<ServiceResult<UpdateProposalItemResponseDto>> UpdateItemAsync(
            Guid proposalItemId,
            Guid currentUserId,
            UpdateProposalItemRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<UpdateProposalItemResponseDto>.Success(new UpdateProposalItemResponseDto()));

        public Task<ServiceResult<DeleteProposalItemResponseDto>> DeleteItemAsync(
            Guid proposalItemId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<DeleteProposalItemResponseDto>.Success(new DeleteProposalItemResponseDto()));

        public Task<ServiceResult<SyncProposalItemsFromSceneResponseDto>> SyncItemsFromSceneAsync(
            Guid proposalId,
            Guid currentUserId,
            SyncProposalItemsFromSceneRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<SyncProposalItemsFromSceneResponseDto>.Success(new SyncProposalItemsFromSceneResponseDto()));

        public Task<ServiceResult<SelectFinalProposalResponseDto>> SelectFinalAsync(
            Guid proposalId,
            Guid currentUserId,
            SelectFinalProposalRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<SelectFinalProposalResponseDto>.Success(new SelectFinalProposalResponseDto()));

        public Task<ServiceResult<RequestProposalRevisionResponseDto>> RequestRevisionAsync(
            Guid proposalId,
            Guid currentUserId,
            RequestProposalRevisionRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<RequestProposalRevisionResponseDto>.Success(new RequestProposalRevisionResponseDto()));

        public Task<ServiceResult<PublishProposalResponseDto>> PublishAsync(
            Guid proposalId,
            Guid currentUserId,
            PublishProposalRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<PublishProposalResponseDto>.Success(new PublishProposalResponseDto()));

        public Task<ServiceResult<UpdateProposalResponseDto>> UpdateAsync(
            Guid proposalId,
            Guid currentUserId,
            UpdateProposalRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<UpdateProposalResponseDto>.Success(new UpdateProposalResponseDto()));

        public Task<ServiceResult<ReopenProposalForEditingResponseDto>> ReopenForEditingAsync(
            Guid proposalId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<ReopenProposalForEditingResponseDto>.Success(new ReopenProposalForEditingResponseDto()));

        public Task<ServiceResult<UpdateProposalSceneResponseDto>> UpdateSceneAsync(
            Guid sceneId,
            Guid currentUserId,
            UpdateProposalSceneRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<UpdateProposalSceneResponseDto>.Success(new UpdateProposalSceneResponseDto()));
    }

    private sealed class FakeProjectChatMessageService : IProjectChatMessageService
    {
        private readonly ServiceResult<ProjectChatMessageSearchResponseDto>? _searchResult;

        public FakeProjectChatMessageService(
            ServiceResult<ProjectChatMessageSearchResponseDto>? searchResult = null)
        {
            _searchResult = searchResult;
        }

        public Guid ProjectId { get; private set; }
        public Guid CurrentUserId { get; private set; }
        public string? SearchQuery { get; private set; }
        public int SearchPage { get; private set; }
        public int SearchLimit { get; private set; }

        public Task<bool> CanAccessChatAsync(
            Guid chatId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<ServiceResult<ProjectChatMessageListResponseDto>> GetMessagesAsync(
            Guid chatId,
            Guid currentUserId,
            ProjectChatMessageQueryDto query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<ProjectChatMessageListResponseDto>.Success(
                new ProjectChatMessageListResponseDto(),
                string.Empty));

        public Task<ServiceResult<ProjectChatMessageDto>> SendTextMessageAsync(
            Guid chatId,
            Guid currentUserId,
            SendTextChatMessageRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<ProjectChatMessageDto>.Created(new ProjectChatMessageDto()));

        public Task<ServiceResult<ProjectChatMessageDto>> SendFileMessageAsync(
            Guid chatId,
            Guid currentUserId,
            SendFileChatMessageRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<ProjectChatMessageDto>.Created(new ProjectChatMessageDto()));

        public Task<ServiceResult<ProjectChatMessageSearchResponseDto>> SearchProjectMessagesAsync(
            Guid projectId,
            Guid currentUserId,
            string query,
            int page,
            int limit,
            CancellationToken cancellationToken = default)
        {
            ProjectId = projectId;
            CurrentUserId = currentUserId;
            SearchQuery = query;
            SearchPage = page;
            SearchLimit = limit;
            return Task.FromResult(_searchResult ?? ServiceResult<ProjectChatMessageSearchResponseDto>.Success(
                new ProjectChatMessageSearchResponseDto(),
                string.Empty));
        }
    }

    private sealed class RecordingMeasurementImageService : IMeasurementImageService
    {
        private readonly ServiceResult<FurniSpace.Application.DTOs.MeasurementImages.MeasurementImageGalleryResponseDto> _galleryResult;

        public RecordingMeasurementImageService(
            ServiceResult<FurniSpace.Application.DTOs.MeasurementImages.MeasurementImageGalleryResponseDto> galleryResult)
        {
            _galleryResult = galleryResult;
        }

        public Guid ProjectId { get; private set; }
        public Guid CurrentUserId { get; private set; }
        public FurniSpace.Application.DTOs.MeasurementImages.MeasurementImageGalleryQueryDto? LastQuery { get; private set; }

        public Task<ServiceResult<FurniSpace.Application.DTOs.ProjectFiles.ProjectFileUploadResponseDto>> RegisterMeasurementImageAsync(
            Guid scheduleId,
            Guid currentUserId,
            FurniSpace.Application.DTOs.MeasurementImages.RegisterMeasurementImageRequestDto request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ServiceResult<FurniSpace.Application.DTOs.ProjectFiles.ProjectFileUploadResponseDto>.NotFound());

        public Task<ServiceResult<FurniSpace.Application.DTOs.MeasurementImages.MeasurementImageGalleryResponseDto>> GetProjectMeasurementImagesAsync(
            Guid projectId,
            Guid currentUserId,
            FurniSpace.Application.DTOs.MeasurementImages.MeasurementImageGalleryQueryDto query,
            CancellationToken cancellationToken = default)
        {
            ProjectId = projectId;
            CurrentUserId = currentUserId;
            LastQuery = query;
            return Task.FromResult(_galleryResult);
        }

        public Task<ServiceResult<FurniSpace.Application.DTOs.MeasurementImages.MeasurementImageGalleryResponseDto>> GetScheduleMeasurementImagesAsync(
            Guid scheduleId,
            Guid currentUserId,
            FurniSpace.Application.DTOs.MeasurementImages.MeasurementImageGalleryQueryDto query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ServiceResult<FurniSpace.Application.DTOs.MeasurementImages.MeasurementImageGalleryResponseDto>.NotFound());

        public Task<ServiceResult<FurniSpace.Application.DTOs.MeasurementImages.MeasurementImageGalleryResponseDto>> GetProjectAreaMeasurementImagesAsync(
            Guid projectAreaId,
            Guid currentUserId,
            FurniSpace.Application.DTOs.MeasurementImages.MeasurementImageGalleryQueryDto query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ServiceResult<FurniSpace.Application.DTOs.MeasurementImages.MeasurementImageGalleryResponseDto>.NotFound());

        public Task<ServiceResult<FurniSpace.Application.DTOs.MeasurementImages.MeasurementImageAreaLinkResponseDto>> LinkMeasurementImageToAreaAsync(
            Guid projectAreaId,
            Guid fileId,
            Guid currentUserId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ServiceResult<FurniSpace.Application.DTOs.MeasurementImages.MeasurementImageAreaLinkResponseDto>.NotFound());

        public Task<ServiceResult<FurniSpace.Application.DTOs.MeasurementImages.MeasurementImageAreaLinkResponseDto>> UnlinkMeasurementImageFromAreaAsync(
            Guid projectAreaId,
            Guid fileId,
            Guid currentUserId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ServiceResult<FurniSpace.Application.DTOs.MeasurementImages.MeasurementImageAreaLinkResponseDto>.NotFound());
    }

    private sealed class NoOpMeasurementImageService : IMeasurementImageService
    {
        public Task<ServiceResult<FurniSpace.Application.DTOs.ProjectFiles.ProjectFileUploadResponseDto>> RegisterMeasurementImageAsync(
            Guid scheduleId,
            Guid currentUserId,
            FurniSpace.Application.DTOs.MeasurementImages.RegisterMeasurementImageRequestDto request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ServiceResult<FurniSpace.Application.DTOs.ProjectFiles.ProjectFileUploadResponseDto>.NotFound());

        public Task<ServiceResult<FurniSpace.Application.DTOs.MeasurementImages.MeasurementImageGalleryResponseDto>> GetProjectMeasurementImagesAsync(
            Guid projectId,
            Guid currentUserId,
            FurniSpace.Application.DTOs.MeasurementImages.MeasurementImageGalleryQueryDto query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ServiceResult<FurniSpace.Application.DTOs.MeasurementImages.MeasurementImageGalleryResponseDto>.NotFound());

        public Task<ServiceResult<FurniSpace.Application.DTOs.MeasurementImages.MeasurementImageGalleryResponseDto>> GetScheduleMeasurementImagesAsync(
            Guid scheduleId,
            Guid currentUserId,
            FurniSpace.Application.DTOs.MeasurementImages.MeasurementImageGalleryQueryDto query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ServiceResult<FurniSpace.Application.DTOs.MeasurementImages.MeasurementImageGalleryResponseDto>.NotFound());

        public Task<ServiceResult<FurniSpace.Application.DTOs.MeasurementImages.MeasurementImageGalleryResponseDto>> GetProjectAreaMeasurementImagesAsync(
            Guid projectAreaId,
            Guid currentUserId,
            FurniSpace.Application.DTOs.MeasurementImages.MeasurementImageGalleryQueryDto query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ServiceResult<FurniSpace.Application.DTOs.MeasurementImages.MeasurementImageGalleryResponseDto>.NotFound());

        public Task<ServiceResult<FurniSpace.Application.DTOs.MeasurementImages.MeasurementImageAreaLinkResponseDto>> LinkMeasurementImageToAreaAsync(
            Guid projectAreaId,
            Guid fileId,
            Guid currentUserId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ServiceResult<FurniSpace.Application.DTOs.MeasurementImages.MeasurementImageAreaLinkResponseDto>.NotFound());

        public Task<ServiceResult<FurniSpace.Application.DTOs.MeasurementImages.MeasurementImageAreaLinkResponseDto>> UnlinkMeasurementImageFromAreaAsync(
            Guid projectAreaId,
            Guid fileId,
            Guid currentUserId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ServiceResult<FurniSpace.Application.DTOs.MeasurementImages.MeasurementImageAreaLinkResponseDto>.NotFound());
    }
}
