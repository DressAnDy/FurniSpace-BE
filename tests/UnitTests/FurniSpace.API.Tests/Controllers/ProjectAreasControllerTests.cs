#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers.Projects;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.ProjectAreas;
using FurniSpace.Application.Interfaces.ProjectAreas;
using FurniSpace.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurniSpace.API.Tests.Controllers;

public sealed class ProjectAreasControllerTests
{
    [Fact]
    public void Create_RequiresSalesDesignerAndAdminRoles()
    {
        var attr = GetMethodAuthorize(nameof(ProjectAreasController.Create));

        Assert.NotNull(attr);
        Assert.Equal("SALES,DESIGNER,ADMIN", attr.Roles);
    }

    [Fact]
    public void GetList_RequiresAllProjectParticipantRoles()
    {
        var attr = GetMethodAuthorize(nameof(ProjectAreasController.GetList));

        Assert.NotNull(attr);
        Assert.Equal("CUSTOMER,SALES,DESIGNER,ADMIN", attr.Roles);
    }

    [Fact]
    public void GetDetail_RequiresAllProjectParticipantRoles()
    {
        var attr = GetMethodAuthorize(nameof(ProjectAreasController.GetDetail));

        Assert.NotNull(attr);
        Assert.Equal("CUSTOMER,SALES,DESIGNER,ADMIN", attr.Roles);
    }

    [Fact]
    public void Update_RequiresSalesDesignerAndAdminRoles()
    {
        var attr = GetMethodAuthorize(nameof(ProjectAreasController.Update));

        Assert.NotNull(attr);
        Assert.Equal("SALES,DESIGNER,ADMIN", attr.Roles);
    }

    [Fact]
    public void Cancel_RequiresSalesDesignerAndAdminRoles()
    {
        var attr = GetMethodAuthorize(nameof(ProjectAreasController.Cancel));

        Assert.NotNull(attr);
        Assert.Equal("SALES,DESIGNER,ADMIN", attr.Roles);
    }

    [Fact]
    public async Task Create_PassesProjectIdAndRequest_ToService()
    {
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var response = new ProjectAreaDto { ProjectAreaId = Guid.NewGuid(), ProjectId = projectId };
        var service = new FakeProjectAreaService(ServiceResult<ProjectAreaDto>.Created(response, "Project area created successfully."));
        var controller = BuildController(service, userId);
        var request = new CreateProjectAreaRequestDto
        {
            AreaName = "Main Cafe Area",
            AreaType = ProjectAreaType.ZONE
        };

        var actionResult = await controller.Create(projectId, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(201, objectResult.StatusCode);
        Assert.Equal(projectId, service.LastProjectId);
        Assert.Equal(userId, service.LastCurrentUserId);
        Assert.Same(request, service.LastCreateRequest);
    }

    [Fact]
    public async Task GetList_PassesIncludeCancelled_ToService()
    {
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var service = new FakeProjectAreaService(ServiceResult<IReadOnlyList<ProjectAreaDto>>.Success([]));
        var controller = BuildController(service, userId);

        await controller.GetList(projectId, includeCancelled: true);

        Assert.Equal(projectId, service.LastProjectId);
        Assert.True(service.LastIncludeCancelled);
    }

    [Fact]
    public async Task Cancel_PassesProjectAreaIdAndUserId_ToService()
    {
        var userId = Guid.NewGuid();
        var projectAreaId = Guid.NewGuid();
        var service = new FakeProjectAreaService(ServiceResult<ProjectAreaDto>.Success(new ProjectAreaDto()));
        var controller = BuildController(service, userId);

        await controller.Cancel(projectAreaId);

        Assert.Equal(projectAreaId, service.LastProjectAreaId);
        Assert.Equal(userId, service.LastCurrentUserId);
        Assert.True(service.LastCancelRequested);
    }

    [Fact]
    public async Task Create_ReturnsUnauthorized_WhenUserMissing()
    {
        var controller = BuildController(new FakeProjectAreaService(ServiceResult<ProjectAreaDto>.Success(new ProjectAreaDto())));

        var result = await controller.Create(Guid.NewGuid(), new CreateProjectAreaRequestDto());

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetDetail_PassesProjectAreaIdAndUserId_ToService()
    {
        var userId = Guid.NewGuid();
        var projectAreaId = Guid.NewGuid();
        var service = new FakeProjectAreaService(ServiceResult<ProjectAreaDto>.Success(new ProjectAreaDto()));
        var controller = BuildController(service, userId);

        await controller.GetDetail(projectAreaId);

        Assert.Equal(projectAreaId, service.LastProjectAreaId);
        Assert.Equal(userId, service.LastCurrentUserId);
    }

    [Fact]
    public async Task Update_PassesProjectAreaIdAndRequest_ToService()
    {
        var userId = Guid.NewGuid();
        var projectAreaId = Guid.NewGuid();
        var service = new FakeProjectAreaService(ServiceResult<ProjectAreaDto>.Success(new ProjectAreaDto()));
        var controller = BuildController(service, userId);
        var request = new UpdateProjectAreaRequestDto { AreaName = "Updated Area" };

        await controller.Update(projectAreaId, request);

        Assert.Equal(projectAreaId, service.LastProjectAreaId);
        Assert.Equal(userId, service.LastCurrentUserId);
        Assert.Same(request, service.LastUpdateRequest);
    }

    [Fact]
    public async Task GetList_ReturnsUnauthorized_WhenUserMissing()
    {
        var controller = BuildController(new FakeProjectAreaService(ServiceResult<IReadOnlyList<ProjectAreaDto>>.Success([])));

        var result = await controller.GetList(Guid.NewGuid());

        Assert.IsType<UnauthorizedResult>(result);
    }

    private static ProjectAreasController BuildController(
        IProjectAreaService service,
        Guid? userId = null)
    {
        return new ProjectAreasController(service)
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
    }

    private static AuthorizeAttribute? GetMethodAuthorize(string methodName)
    {
        return typeof(ProjectAreasController)
            .GetMethod(methodName)
            ?.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .SingleOrDefault();
    }

    private sealed class FakeProjectAreaService : IProjectAreaService
    {
        private readonly ServiceResult<ProjectAreaDto> _singleResult;
        private readonly ServiceResult<IReadOnlyList<ProjectAreaDto>> _listResult;

        public FakeProjectAreaService(ServiceResult<ProjectAreaDto> result)
        {
            _singleResult = result;
            _listResult = ServiceResult<IReadOnlyList<ProjectAreaDto>>.Success([]);
        }

        public FakeProjectAreaService(ServiceResult<IReadOnlyList<ProjectAreaDto>> result)
        {
            _singleResult = ServiceResult<ProjectAreaDto>.Success(new ProjectAreaDto());
            _listResult = result;
        }

        public Guid LastProjectId { get; private set; }
        public Guid LastProjectAreaId { get; private set; }
        public Guid LastCurrentUserId { get; private set; }
        public bool LastIncludeCancelled { get; private set; }
        public bool LastCancelRequested { get; private set; }
        public CreateProjectAreaRequestDto? LastCreateRequest { get; private set; }
        public UpdateProjectAreaRequestDto? LastUpdateRequest { get; private set; }

        public Task<ServiceResult<ProjectAreaDto>> CreateAsync(
            Guid projectId,
            Guid currentUserId,
            CreateProjectAreaRequestDto request,
            CancellationToken cancellationToken = default)
        {
            LastProjectId = projectId;
            LastCurrentUserId = currentUserId;
            LastCreateRequest = request;
            return Task.FromResult(_singleResult);
        }

        public Task<ServiceResult<IReadOnlyList<ProjectAreaDto>>> GetListByProjectAsync(
            Guid projectId,
            Guid currentUserId,
            bool includeCancelled,
            CancellationToken cancellationToken = default)
        {
            LastProjectId = projectId;
            LastCurrentUserId = currentUserId;
            LastIncludeCancelled = includeCancelled;
            return Task.FromResult(_listResult);
        }

        public Task<ServiceResult<ProjectAreaDto>> GetDetailAsync(
            Guid projectAreaId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
            => ReturnSingleResult(projectAreaId, currentUserId);

        public Task<ServiceResult<ProjectAreaDto>> UpdateAsync(
            Guid projectAreaId,
            Guid currentUserId,
            UpdateProjectAreaRequestDto request,
            CancellationToken cancellationToken = default)
        {
            LastUpdateRequest = request;
            return ReturnSingleResult(projectAreaId, currentUserId);
        }

        public Task<ServiceResult<ProjectAreaDto>> CancelAsync(
            Guid projectAreaId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            LastCancelRequested = true;
            return ReturnSingleResult(projectAreaId, currentUserId);
        }

        private Task<ServiceResult<ProjectAreaDto>> ReturnSingleResult(Guid projectAreaId, Guid currentUserId)
        {
            LastProjectAreaId = projectAreaId;
            LastCurrentUserId = currentUserId;
            return Task.FromResult(_singleResult);
        }
    }
}
