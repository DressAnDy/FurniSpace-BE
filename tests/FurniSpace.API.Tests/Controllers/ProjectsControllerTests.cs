#nullable enable

using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Projects;
using FurniSpace.Application.Interfaces.Projects;
using FurniSpace.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurniSpace.API.Tests.Controllers;

public sealed class ProjectsControllerTests
{
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
    public void GetList_AllowsSalesAdminAndCustomerRoles()
    {
        var authorize = GetMethodAuthorizeAttribute(nameof(ProjectsController.GetList));

        Assert.NotNull(authorize);
        Assert.Equal("SALES,ADMIN,CUSTOMER", authorize.Roles);
    }

    [Fact]
    public void GetById_AllowsProjectParticipantRoles()
    {
        var authorize = GetMethodAuthorizeAttribute(nameof(ProjectsController.GetById));

        Assert.NotNull(authorize);
        Assert.Equal("SALES,ADMIN,CUSTOMER,DESIGNER", authorize.Roles);
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
    public void UpdateStatus_AllowsSalesAndAdminRoles()
    {
        var authorize = GetMethodAuthorizeAttribute(nameof(ProjectsController.UpdateStatus));

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
        var controller = new ProjectsController(service)
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
        var controller = new ProjectsController(service)
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
        var controller = new ProjectsController(service)
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
        var controller = new ProjectsController(service)
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
        var controller = new ProjectsController(service)
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
        var controller = new ProjectsController(service)
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
    public async Task UpdateBasicInformation_WithoutUserIdClaim_ReturnsUnauthorized()
    {
        var service = new FakeProjectService(ServiceResult<ProjectDto>.Created(new ProjectDto()));
        var controller = new ProjectsController(service)
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
        var controller = new ProjectsController(service)
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
        var controller = new ProjectsController(service)
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
        var controller = new ProjectsController(service)
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

    private static AuthorizeAttribute? GetMethodAuthorizeAttribute(string methodName)
    {
        return typeof(ProjectsController)
            .GetMethods()
            .Single(method => method.Name == methodName)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .SingleOrDefault();
    }

    private static ProjectsController CreateControllerWithUser(FakeProjectService service, Guid currentUserId)
    {
        return new ProjectsController(service)
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
        private readonly ServiceResult<ProjectStatusUpdateDto> _updateStatusResult;
        private readonly ServiceResult<ProjectRejectionDto> _rejectResult;
        private readonly ServiceResult<ProjectDesignerAssignmentDto> _assignDesignerResult;

        public FakeProjectService(
            ServiceResult<ProjectDto> createResult,
            ServiceResult<ProjectListResponseDto>? listResult = null,
            ServiceResult<ProjectDto>? detailResult = null,
            ServiceResult<ProjectSalesAssignmentDto>? assignSalesResult = null,
            ServiceResult<ProjectInformationRequestDto>? requestInformationResult = null,
            ServiceResult<ProjectBasicInformationDto>? updateBasicInformationResult = null,
            ServiceResult<ProjectStatusUpdateDto>? updateStatusResult = null,
            ServiceResult<ProjectRejectionDto>? rejectResult = null,
            ServiceResult<ProjectDesignerAssignmentDto>? assignDesignerResult = null)
        {
            _createResult = createResult;
            _listResult = listResult ?? ServiceResult<ProjectListResponseDto>.Success(new ProjectListResponseDto());
            _detailResult = detailResult ?? ServiceResult<ProjectDto>.Success(new ProjectDto());
            _assignSalesResult = assignSalesResult ?? ServiceResult<ProjectSalesAssignmentDto>.Success(new ProjectSalesAssignmentDto());
            _requestInformationResult = requestInformationResult ??
                ServiceResult<ProjectInformationRequestDto>.Success(new ProjectInformationRequestDto());
            _updateBasicInformationResult = updateBasicInformationResult ??
                ServiceResult<ProjectBasicInformationDto>.Success(new ProjectBasicInformationDto());
            _updateStatusResult = updateStatusResult ??
                ServiceResult<ProjectStatusUpdateDto>.Success(new ProjectStatusUpdateDto());
            _rejectResult = rejectResult ??
                ServiceResult<ProjectRejectionDto>.Success(new ProjectRejectionDto());
            _assignDesignerResult = assignDesignerResult ??
                ServiceResult<ProjectDesignerAssignmentDto>.Success(new ProjectDesignerAssignmentDto());
        }

        public Guid CurrentUserId { get; private set; }
        public Guid ProjectId { get; private set; }
        public CreateProjectRequestDto? Request { get; private set; }
        public AssignProjectSalesRequestDto? AssignSalesRequest { get; private set; }
        public RequestProjectInformationRequestDto? RequestInformationRequest { get; private set; }
        public UpdateProjectBasicInformationRequestDto? UpdateBasicInformationRequest { get; private set; }
        public UpdateProjectStatusRequestDto? UpdateStatusRequest { get; private set; }
        public RejectProjectRequestDto? RejectRequest { get; private set; }
        public AssignProjectDesignerRequestDto? AssignDesignerRequest { get; private set; }
        public ProjectListQueryDto? Query { get; private set; }

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
    }
}
