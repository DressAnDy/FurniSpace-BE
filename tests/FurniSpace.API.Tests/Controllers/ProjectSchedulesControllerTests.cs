#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers.Projects;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.ProjectSchedules;
using FurniSpace.Application.Interfaces.ProjectSchedules;
using FurniSpace.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurniSpace.API.Tests.Controllers;

public sealed class ProjectSchedulesControllerTests
{
    // ── Attribute checks ─────────────────────────────────────────────────────────

    [Fact]
    public void Controller_RequiresAuthorization()
    {
        var attr = typeof(ProjectSchedulesController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .SingleOrDefault();

        Assert.NotNull(attr);
        Assert.Null(attr.Roles);
    }

    [Fact]
    public void Create_RequiresSalesProductionAndAdminRoles()
    {
        var attr = GetMethodAuthorize<ProjectSchedulesController>(nameof(ProjectSchedulesController.Create));

        Assert.NotNull(attr);
        Assert.Equal("SALES,PRODUCTION,ADMIN", attr.Roles);
    }

    [Fact]
    public void GetList_RequiresAllProjectParticipantRoles()
    {
        var attr = GetMethodAuthorize<ProjectSchedulesController>(nameof(ProjectSchedulesController.GetList));

        Assert.NotNull(attr);
        Assert.Equal("CUSTOMER,SALES,DESIGNER,PRODUCTION,ADMIN", attr.Roles);
    }

    [Fact]
    public void GetMyAssigned_RequiresSalesDesignerProductionAndAdminRoles()
    {
        var attr = GetMethodAuthorize<ProjectSchedulesController>(nameof(ProjectSchedulesController.GetMyAssigned));

        Assert.NotNull(attr);
        Assert.Equal("SALES,DESIGNER,PRODUCTION,ADMIN", attr.Roles);
    }

    [Fact]
    public void GetDetail_RequiresProjectParticipantRoles()
    {
        var attr = GetMethodAuthorize<ProjectSchedulesController>(nameof(ProjectSchedulesController.GetDetail));

        Assert.NotNull(attr);
        Assert.Equal("CUSTOMER,SALES,DESIGNER,PRODUCTION,ADMIN", attr.Roles);
    }

    [Fact]
    public void Update_RequiresSalesProductionAndAdminRoles()
    {
        var attr = GetMethodAuthorize<ProjectSchedulesController>(nameof(ProjectSchedulesController.Update));

        Assert.NotNull(attr);
        Assert.Equal("SALES,PRODUCTION,ADMIN", attr.Roles);
    }

    [Fact]
    public void UpdateStatus_RequiresCustomerSalesDesignerProductionAndAdminRoles()
    {
        var attr = GetMethodAuthorize<ProjectSchedulesController>(nameof(ProjectSchedulesController.UpdateStatus));

        Assert.NotNull(attr);
        Assert.Equal("CUSTOMER,SALES,DESIGNER,PRODUCTION,ADMIN", attr.Roles);
    }

    [Fact]
    public void Delete_RequiresSalesProductionAndAdminRoles()
    {
        var attr = GetMethodAuthorize<ProjectSchedulesController>(nameof(ProjectSchedulesController.Delete));

        Assert.NotNull(attr);
        Assert.Equal("SALES,PRODUCTION,ADMIN", attr.Roles);
    }

    // ── Create endpoint ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_WithoutUserIdClaim_ReturnsUnauthorized()
    {
        var service = new FakeProjectScheduleService(ServiceResult<ProjectScheduleDto>.Created(new ProjectScheduleDto()));
        var controller = BuildController(service);

        var actionResult = await controller.Create(Guid.NewGuid(), new CreateProjectScheduleRequestDto());

        Assert.IsType<UnauthorizedResult>(actionResult);
    }

    [Fact]
    public async Task Create_PassesProjectIdAndRequest_ToService()
    {
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var response = new ProjectScheduleDto { ScheduleId = Guid.NewGuid(), ProjectId = projectId };
        var service = new FakeProjectScheduleService(ServiceResult<ProjectScheduleDto>.Created(response, "Schedule created."));
        var controller = BuildController(service, userId);
        var request = new CreateProjectScheduleRequestDto
        {
            ScheduleType = ProjectScheduleType.MEASUREMENT,
            ScheduledStart = DateTime.UtcNow.AddDays(1)
        };

        var actionResult = await controller.Create(projectId, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(201, objectResult.StatusCode);
        Assert.Equal(projectId, service.LastProjectId);
        Assert.Equal(userId, service.LastCurrentUserId);
        Assert.Same(request, service.LastCreateRequest);
    }

    // ── GetList endpoint ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetList_WithoutUserIdClaim_ReturnsUnauthorized()
    {
        var service = new FakeProjectScheduleService(
            ServiceResult<ProjectScheduleListResponseDto>.Success(new ProjectScheduleListResponseDto()));
        var controller = BuildController(service);

        var actionResult = await controller.GetList(Guid.NewGuid(), new ProjectScheduleListQueryDto());

        Assert.IsType<UnauthorizedResult>(actionResult);
    }

    [Fact]
    public async Task GetList_PassesQueryParameters_ToService()
    {
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var service = new FakeProjectScheduleService(
            ServiceResult<ProjectScheduleListResponseDto>.Success(new ProjectScheduleListResponseDto()));
        var controller = BuildController(service, userId);

        var actionResult = await controller.GetList(
            projectId,
            new ProjectScheduleListQueryDto
            {
                ScheduleType = ProjectScheduleType.DELIVERY,
                Status = ProjectScheduleStatus.CONFIRMED,
                Page = 2,
                Limit = 10
            });

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(projectId, service.LastProjectId);
        Assert.Equal(userId, service.LastCurrentUserId);
        Assert.NotNull(service.LastListQuery);
        Assert.Equal(ProjectScheduleType.DELIVERY, service.LastListQuery.ScheduleType);
        Assert.Equal(ProjectScheduleStatus.CONFIRMED, service.LastListQuery.Status);
        Assert.Equal(2, service.LastListQuery.Page);
        Assert.Equal(10, service.LastListQuery.Limit);
    }

    // ── GetDetail endpoint ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetDetail_WithoutUserIdClaim_ReturnsUnauthorized()
    {
        var service = new FakeProjectScheduleService(ServiceResult<ProjectScheduleDto>.Success(new ProjectScheduleDto()));
        var controller = BuildController(service);

        var actionResult = await controller.GetDetail(Guid.NewGuid());

        Assert.IsType<UnauthorizedResult>(actionResult);
    }

    [Fact]
    public async Task GetDetail_PassesScheduleIdAndUserId_ToService()
    {
        var userId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        var response = new ProjectScheduleDto { ScheduleId = scheduleId };
        var service = new FakeProjectScheduleService(ServiceResult<ProjectScheduleDto>.Success(response));
        var controller = BuildController(service, userId);

        var actionResult = await controller.GetDetail(scheduleId);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(scheduleId, service.LastScheduleId);
        Assert.Equal(userId, service.LastCurrentUserId);
    }

    // ── Update endpoint ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_WithoutUserIdClaim_ReturnsUnauthorized()
    {
        var service = new FakeProjectScheduleService(ServiceResult<ProjectScheduleDto>.Success(new ProjectScheduleDto()));
        var controller = BuildController(service);

        var actionResult = await controller.Update(Guid.NewGuid(), new UpdateProjectScheduleRequestDto());

        Assert.IsType<UnauthorizedResult>(actionResult);
    }

    [Fact]
    public async Task Update_PassesRequestToService()
    {
        var userId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        var service = new FakeProjectScheduleService(ServiceResult<ProjectScheduleDto>.Success(new ProjectScheduleDto()));
        var controller = BuildController(service, userId);
        var request = new UpdateProjectScheduleRequestDto { Title = "Updated" };

        await controller.Update(scheduleId, request);

        Assert.Equal(scheduleId, service.LastScheduleId);
        Assert.Same(request, service.LastUpdateRequest);
    }

    // ── UpdateStatus endpoint ────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateStatus_WithoutUserIdClaim_ReturnsUnauthorized()
    {
        var service = new FakeProjectScheduleService(ServiceResult<ProjectScheduleDto>.Success(new ProjectScheduleDto()));
        var controller = BuildController(service);

        var actionResult = await controller.UpdateStatus(Guid.NewGuid(), new UpdateProjectScheduleStatusRequestDto());

        Assert.IsType<UnauthorizedResult>(actionResult);
    }

    [Fact]
    public async Task Delete_PassesScheduleIdAndUserId_ToService()
    {
        var userId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        var service = new FakeProjectScheduleService(ServiceResult<ProjectScheduleDto>.Success(new ProjectScheduleDto()));
        var controller = BuildController(service, userId);

        var actionResult = await controller.Delete(scheduleId);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(scheduleId, service.LastScheduleId);
        Assert.Equal(userId, service.LastCurrentUserId);
    }

    // ── GetMyAssigned endpoint ───────────────────────────────────────────────────

    [Fact]
    public async Task GetMyAssigned_WithoutUserIdClaim_ReturnsUnauthorized()
    {
        var service = new FakeProjectScheduleService(
            ServiceResult<ProjectScheduleListResponseDto>.Success(new ProjectScheduleListResponseDto()));
        var controller = BuildController(service);

        var actionResult = await controller.GetMyAssigned();

        Assert.IsType<UnauthorizedResult>(actionResult);
    }

    [Fact]
    public async Task GetMyAssigned_PassesUserIdAndQuery_ToService()
    {
        var userId = Guid.NewGuid();
        var service = new FakeProjectScheduleService(
            ServiceResult<ProjectScheduleListResponseDto>.Success(new ProjectScheduleListResponseDto { Total = 3 }));
        var controller = BuildController(service, userId);

        var actionResult = await controller.GetMyAssigned(
            scheduleType: ProjectScheduleType.HANDOVER,
            page: 3,
            limit: 5);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(userId, service.LastCurrentUserId);
        Assert.NotNull(service.LastListQuery);
        Assert.Equal(ProjectScheduleType.HANDOVER, service.LastListQuery.ScheduleType);
        Assert.Equal(3, service.LastListQuery.Page);
        Assert.Equal(5, service.LastListQuery.Limit);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static ProjectSchedulesController BuildController(
        IProjectScheduleService service,
        Guid? userId = null)
    {
        var controller = new ProjectSchedulesController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = userId.HasValue
                        ? new ClaimsPrincipal(new ClaimsIdentity(
                            [new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString())], "TestAuth"))
                        : new ClaimsPrincipal()
                }
            }
        };
        return controller;
    }

    private static AuthorizeAttribute? GetMethodAuthorize<TController>(string methodName)
    {
        return typeof(TController)
            .GetMethod(methodName)
            ?.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .SingleOrDefault();
    }

    // ── Fake service ─────────────────────────────────────────────────────────────

    private sealed class FakeProjectScheduleService : IProjectScheduleService
    {
        private readonly ServiceResult<ProjectScheduleDto> _singleResult;
        private readonly ServiceResult<ProjectScheduleListResponseDto> _listResult;

        public FakeProjectScheduleService(ServiceResult<ProjectScheduleDto> result)
        {
            _singleResult = result;
            _listResult = ServiceResult<ProjectScheduleListResponseDto>.Success(new ProjectScheduleListResponseDto());
        }

        public FakeProjectScheduleService(ServiceResult<ProjectScheduleListResponseDto> result)
        {
            _singleResult = ServiceResult<ProjectScheduleDto>.Success(new ProjectScheduleDto());
            _listResult = result;
        }

        public Guid LastProjectId { get; private set; }
        public Guid LastScheduleId { get; private set; }
        public Guid LastCurrentUserId { get; private set; }
        public CreateProjectScheduleRequestDto? LastCreateRequest { get; private set; }
        public UpdateProjectScheduleRequestDto? LastUpdateRequest { get; private set; }
        public ProjectScheduleListQueryDto? LastListQuery { get; private set; }

        public Task<ServiceResult<ProjectScheduleDto>> CreateAsync(
            Guid projectId, Guid currentUserId, CreateProjectScheduleRequestDto request,
            CancellationToken cancellationToken = default)
        {
            LastProjectId = projectId;
            LastCurrentUserId = currentUserId;
            LastCreateRequest = request;
            return Task.FromResult(_singleResult);
        }

        public Task<ServiceResult<ProjectScheduleListResponseDto>> GetListByProjectAsync(
            Guid projectId, Guid currentUserId, ProjectScheduleListQueryDto query,
            CancellationToken cancellationToken = default)
        {
            LastProjectId = projectId;
            LastCurrentUserId = currentUserId;
            LastListQuery = query;
            return Task.FromResult(_listResult);
        }

        public Task<ServiceResult<ProjectScheduleDto>> GetDetailAsync(
            Guid scheduleId, Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            LastScheduleId = scheduleId;
            LastCurrentUserId = currentUserId;
            return Task.FromResult(_singleResult);
        }

        public Task<ServiceResult<ProjectScheduleDto>> UpdateAsync(
            Guid scheduleId, Guid currentUserId, UpdateProjectScheduleRequestDto request,
            CancellationToken cancellationToken = default)
        {
            LastScheduleId = scheduleId;
            LastCurrentUserId = currentUserId;
            LastUpdateRequest = request;
            return Task.FromResult(_singleResult);
        }

        public Task<ServiceResult<ProjectScheduleDto>> UpdateStatusAsync(
            Guid scheduleId, Guid currentUserId, UpdateProjectScheduleStatusRequestDto request,
            CancellationToken cancellationToken = default)
        {
            LastScheduleId = scheduleId;
            LastCurrentUserId = currentUserId;
            return Task.FromResult(_singleResult);
        }

        public Task<ServiceResult<ProjectScheduleDto>> DeleteAsync(
            Guid scheduleId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            LastScheduleId = scheduleId;
            LastCurrentUserId = currentUserId;
            return Task.FromResult(_singleResult);
        }

        public Task<ServiceResult<ProjectScheduleListResponseDto>> GetMyAssignedAsync(
            Guid currentUserId, ProjectScheduleListQueryDto query,
            CancellationToken cancellationToken = default)
        {
            LastCurrentUserId = currentUserId;
            LastListQuery = query;
            return Task.FromResult(_listResult);
        }
    }
}
