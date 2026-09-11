#nullable enable

using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers.Projects;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Projects;
using FurniSpace.Application.Interfaces.Projects;
using FurniSpace.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurniSpace.API.Tests.Controllers;

public sealed class ProjectPhaseDeadlinesControllerTests
{
    [Fact]
    public void Controller_RequiresAuthorization()
    {
        var authorize = typeof(ProjectPhaseDeadlinesController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .SingleOrDefault();

        Assert.NotNull(authorize);
        Assert.Null(authorize.Roles);
    }

    [Fact]
    public void Get_AllowsProjectParticipantRoles()
    {
        var authorize = GetMethodAuthorizeAttribute(nameof(ProjectPhaseDeadlinesController.Get));

        Assert.Equal("CUSTOMER,SALES,DESIGNER,PRODUCTION,ADMIN", authorize.Roles);
    }

    [Fact]
    public void Upsert_AllowsSalesAndAdmin()
    {
        var authorize = GetMethodAuthorizeAttribute(nameof(ProjectPhaseDeadlinesController.Upsert));

        Assert.Equal("SALES,ADMIN", authorize.Roles);
    }

    [Fact]
    public async Task Get_ReturnsServiceResultThroughBaseController()
    {
        var projectId = Guid.NewGuid();
        var response = new ProjectPhaseDeadlinePlanDto { ProjectId = projectId };
        var service = new FakeProjectPhaseDeadlineService(
            getResult: ServiceResult<ProjectPhaseDeadlinePlanDto>.Success(response));
        var controller = WithUser(new ProjectPhaseDeadlinesController(service));

        var actionResult = await controller.Get(projectId);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        var result = Assert.IsType<ServiceResult<ProjectPhaseDeadlinePlanDto>>(objectResult.Value);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Same(response, result.Data);
        Assert.Equal(projectId, service.GetProjectId);
    }

    [Fact]
    public async Task Upsert_PassesRequestToService()
    {
        var projectId = Guid.NewGuid();
        var request = new UpsertProjectPhaseDeadlinesRequestDto
        {
            ProposalDueDate = new DateOnly(2026, 9, 10),
            ProductionDueDate = new DateOnly(2026, 9, 25)
        };
        var service = new FakeProjectPhaseDeadlineService(
            upsertResult: ServiceResult<ProjectPhaseDeadlinePlanDto>.Success(new ProjectPhaseDeadlinePlanDto()));
        var controller = WithUser(new ProjectPhaseDeadlinesController(service));

        var actionResult = await controller.Upsert(projectId, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Same(request, service.UpsertRequest);
        Assert.Equal(projectId, service.UpsertProjectId);
    }

    [Fact]
    public async Task Get_WithoutUserClaim_ReturnsUnauthorized()
    {
        var controller = new ProjectPhaseDeadlinesController(new FakeProjectPhaseDeadlineService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.Get(Guid.NewGuid());

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public void UpsertProductionDeadline_AllowsSalesAndAdmin()
    {
        var authorize = GetMethodAuthorizeAttribute(nameof(ProjectPhaseDeadlinesController.UpsertProductionDeadline));

        Assert.Equal("SALES,ADMIN", authorize.Roles);
    }

    [Fact]
    public async Task UpsertProductionDeadline_PassesRequestToService()
    {
        var projectId = Guid.NewGuid();
        var request = new UpsertProductionPhaseDeadlineRequestDto
        {
            ProductionDeadline = new DateOnly(2026, 9, 25)
        };
        var response = new ProjectProductionPhaseDeadlineResponseDto
        {
            ProjectId = projectId,
            DueDate = request.ProductionDeadline.Value
        };
        var service = new FakeProjectPhaseDeadlineService(
            upsertProductionResult: ServiceResult<ProjectProductionPhaseDeadlineResponseDto>.Success(response));
        var controller = WithUser(new ProjectPhaseDeadlinesController(service));

        var actionResult = await controller.UpsertProductionDeadline(projectId, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        var result = Assert.IsType<ServiceResult<ProjectProductionPhaseDeadlineResponseDto>>(objectResult.Value);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Same(request, service.UpsertProductionRequest);
        Assert.Equal(projectId, service.UpsertProductionProjectId);
        Assert.Same(response, result.Data);
    }

    [Fact]
    public async Task UpsertProductionDeadline_WithoutUserClaim_ReturnsUnauthorized()
    {
        var controller = new ProjectPhaseDeadlinesController(new FakeProjectPhaseDeadlineService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.UpsertProductionDeadline(
            Guid.NewGuid(),
            new UpsertProductionPhaseDeadlineRequestDto
            {
                ProductionDeadline = new DateOnly(2026, 9, 25)
            });

        Assert.IsType<UnauthorizedResult>(result);
    }

    private static AuthorizeAttribute GetMethodAuthorizeAttribute(string methodName)
    {
        return typeof(ProjectPhaseDeadlinesController)
            .GetMethod(methodName)!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single();
    }

    private static ProjectPhaseDeadlinesController WithUser(ProjectPhaseDeadlinesController controller)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())],
            "Test"));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
        return controller;
    }

    private sealed class FakeProjectPhaseDeadlineService : IProjectPhaseDeadlineService
    {
        private readonly ServiceResult<ProjectPhaseDeadlinePlanDto> _getResult;
        private readonly ServiceResult<ProjectPhaseDeadlinePlanDto> _upsertResult;
        private readonly ServiceResult<ProjectProductionPhaseDeadlineResponseDto> _upsertProductionResult;

        public FakeProjectPhaseDeadlineService(
            ServiceResult<ProjectPhaseDeadlinePlanDto>? getResult = null,
            ServiceResult<ProjectPhaseDeadlinePlanDto>? upsertResult = null,
            ServiceResult<ProjectProductionPhaseDeadlineResponseDto>? upsertProductionResult = null)
        {
            _getResult = getResult ?? ServiceResult<ProjectPhaseDeadlinePlanDto>.Success(new ProjectPhaseDeadlinePlanDto());
            _upsertResult = upsertResult ?? ServiceResult<ProjectPhaseDeadlinePlanDto>.Success(new ProjectPhaseDeadlinePlanDto());
            _upsertProductionResult = upsertProductionResult ??
                ServiceResult<ProjectProductionPhaseDeadlineResponseDto>.Success(new ProjectProductionPhaseDeadlineResponseDto());
        }

        public Guid GetProjectId { get; private set; }
        public Guid UpsertProjectId { get; private set; }
        public UpsertProjectPhaseDeadlinesRequestDto? UpsertRequest { get; private set; }
        public Guid UpsertProductionProjectId { get; private set; }
        public UpsertProductionPhaseDeadlineRequestDto? UpsertProductionRequest { get; private set; }

        public Task<ServiceResult<ProjectPhaseDeadlinePlanDto>> GetAsync(
            Guid projectId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            GetProjectId = projectId;
            return Task.FromResult(_getResult);
        }

        public Task<ServiceResult<ProjectPhaseDeadlinePlanDto>> UpsertAsync(
            Guid projectId,
            Guid currentUserId,
            UpsertProjectPhaseDeadlinesRequestDto request,
            CancellationToken cancellationToken = default)
        {
            UpsertProjectId = projectId;
            UpsertRequest = request;
            return Task.FromResult(_upsertResult);
        }

        public Task MarkStartedOnceAsync(
            Guid projectId,
            ProjectPhaseType phase,
            DateTime startedAt,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task MarkCompletedOnceAsync(
            Guid projectId,
            ProjectPhaseType phase,
            DateTime completedAt,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<ServiceResult<ProjectProductionPhaseDeadlineResponseDto>> UpsertProductionDeadlineAsync(
            Guid projectId,
            Guid currentUserId,
            UpsertProductionPhaseDeadlineRequestDto request,
            CancellationToken cancellationToken = default)
        {
            UpsertProductionProjectId = projectId;
            UpsertProductionRequest = request;
            return Task.FromResult(_upsertProductionResult);
        }

        public Task<ServiceResult<DateOnly>> StageProposalDeadlineForDesignerAssignmentAsync(
            Guid projectId,
            Guid currentUserId,
            DateOnly proposalDeadline,
            DateOnly? targetCompletionDate,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ServiceResult<DateOnly>.Success(proposalDeadline));
        }

        public Task<bool> HasProductionDeadlineAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<DateOnly?> GetProductionDeadlineAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<DateOnly?>(null);
        }
    }
}
