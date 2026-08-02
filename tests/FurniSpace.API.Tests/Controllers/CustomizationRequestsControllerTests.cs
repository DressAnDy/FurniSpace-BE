#nullable enable

using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers.Projects;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.CustomizationRequests;
using FurniSpace.Application.Interfaces.CustomizationRequests;
using FurniSpace.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurniSpace.API.Tests.Controllers;

public sealed class CustomizationRequestsControllerTests
{
    [Fact]
    public void Controller_RequiresAuthorization()
    {
        var authorize = typeof(CustomizationRequestsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Null(authorize.Roles);
    }

    [Theory]
    [InlineData(nameof(CustomizationRequestsController.GetByProject), "CUSTOMER,SALES,DESIGNER,PRODUCTION,ADMIN")]
    [InlineData(nameof(CustomizationRequestsController.GetDetail), "CUSTOMER,SALES,DESIGNER,PRODUCTION,ADMIN")]
    [InlineData(nameof(CustomizationRequestsController.GetVersions), "CUSTOMER,SALES,DESIGNER,PRODUCTION,ADMIN")]
    [InlineData(nameof(CustomizationRequestsController.GetVersionDetail), "CUSTOMER,SALES,DESIGNER,PRODUCTION,ADMIN")]
    [InlineData(nameof(CustomizationRequestsController.Submit), "CUSTOMER,DESIGNER,ADMIN")]
    [InlineData(nameof(CustomizationRequestsController.CreateVersion), "DESIGNER,ADMIN")]
    [InlineData(nameof(CustomizationRequestsController.AcceptVersion), "CUSTOMER")]
    [InlineData(nameof(CustomizationRequestsController.Cancel), "CUSTOMER,SALES,DESIGNER,ADMIN")]
    public void Actions_UseExpectedRoles(string actionName, string expectedRoles)
    {
        var authorize = typeof(CustomizationRequestsController)
            .GetMethods()
            .Single(method => method.Name == actionName)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal(expectedRoles, authorize.Roles);
    }

    [Fact]
    public async Task GetByProject_ReturnsServiceResultAndPassesQuery()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var sourceProductVersionId = Guid.NewGuid();
        var service = new FakeCustomizationRequestService();
        var controller = BuildController(service, userId);

        var actionResult = await controller.GetByProject(
            projectId,
            proposalId,
            sourceProductVersionId,
            CustomizationStatus.SUBMITTED);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(projectId, service.ProjectId);
        Assert.Equal(userId, service.CurrentUserId);
        Assert.Equal(proposalId, service.Query!.ProposalId);
        Assert.Equal(sourceProductVersionId, service.Query.SourceProductVersionId);
        Assert.Equal(CustomizationStatus.SUBMITTED, service.Query.Status);
    }

    [Fact]
    public async Task CreateVersion_ReturnsServiceResultAndPassesRequest()
    {
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var request = new CreateCustomizationRequestVersionDto { VersionName = "Custom Chair" };
        var service = new FakeCustomizationRequestService
        {
            CreateVersionResult = ServiceResult<CreateCustomizationRequestVersionResponseDto>.Created(
                new CreateCustomizationRequestVersionResponseDto())
        };
        var controller = BuildController(service, userId);

        var actionResult = await controller.CreateVersion(requestId, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(201, objectResult.StatusCode);
        Assert.Equal(requestId, service.CustomizationRequestId);
        Assert.Same(request, service.CreateVersionRequest);
    }

    [Fact]
    public async Task AcceptVersion_ReturnsServiceResultAndPassesRequest()
    {
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var request = new AcceptCustomizationRequestDto { CustomizationRequestVersionId = Guid.NewGuid() };
        var service = new FakeCustomizationRequestService();
        var controller = BuildController(service, userId);

        var actionResult = await controller.AcceptVersion(requestId, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(requestId, service.CustomizationRequestId);
        Assert.Same(request, service.AcceptVersionRequest);
    }

    [Fact]
    public async Task GetVersions_ReturnsServiceResultAndPassesIds()
    {
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var service = new FakeCustomizationRequestService
        {
            VersionsResult = ServiceResult<CustomizationRequestVersionListResponseDto>.Success(
                new CustomizationRequestVersionListResponseDto { Items = [] })
        };
        var controller = BuildController(service, userId);

        var actionResult = await controller.GetVersions(requestId);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(requestId, service.CustomizationRequestId);
        Assert.Equal(userId, service.CurrentUserId);
    }

    [Fact]
    public async Task GetVersionDetail_ReturnsServiceResultAndPassesIds()
    {
        var requestId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var service = new FakeCustomizationRequestService
        {
            VersionDetailResult = ServiceResult<CustomizationRequestVersionDto>.Success(new CustomizationRequestVersionDto())
        };
        var controller = BuildController(service, userId);

        var actionResult = await controller.GetVersionDetail(requestId, versionId);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(requestId, service.CustomizationRequestId);
        Assert.Equal(versionId, service.CustomizationRequestVersionId);
    }

    [Fact]
    public async Task UpdateDraftVersion_ReturnsServiceResultAndPassesRequest()
    {
        var requestId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var request = new UpdateCustomizationRequestVersionDto { VersionName = "Updated Chair" };
        var service = new FakeCustomizationRequestService
        {
            UpdateDraftVersionResult = ServiceResult<CustomizationRequestVersionDto>.Success(new CustomizationRequestVersionDto())
        };
        var controller = BuildController(service, userId);

        var actionResult = await controller.UpdateDraftVersion(requestId, versionId, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(requestId, service.CustomizationRequestId);
        Assert.Equal(versionId, service.CustomizationRequestVersionId);
        Assert.Same(request, service.UpdateDraftVersionRequest);
    }

    [Fact]
    public async Task GetByProject_WithoutUserClaim_ReturnsUnauthorized()
    {
        var controller = BuildController(new FakeCustomizationRequestService(), userId: null);

        var actionResult = await controller.GetByProject(Guid.NewGuid());

        Assert.IsType<UnauthorizedResult>(actionResult);
    }

    private static CustomizationRequestsController BuildController(
        ICustomizationRequestService service,
        Guid? userId)
    {
        var controller = new CustomizationRequestsController(service);
        if (userId.HasValue)
        {
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString())
                    ], "Test"))
                }
            };
        }

        return controller;
    }

    private sealed class FakeCustomizationRequestService : ICustomizationRequestService
    {
        public ServiceResult<CustomizationRequestListResponseDto> ListResult { get; init; } =
            ServiceResult<CustomizationRequestListResponseDto>.Success(new CustomizationRequestListResponseDto());

        public ServiceResult<CustomizationRequestDto> DetailResult { get; init; } =
            ServiceResult<CustomizationRequestDto>.Success(new CustomizationRequestDto());

        public ServiceResult<CustomizationRequestVersionListResponseDto> VersionsResult { get; init; } =
            ServiceResult<CustomizationRequestVersionListResponseDto>.Success(new CustomizationRequestVersionListResponseDto());

        public ServiceResult<CustomizationRequestVersionDto> VersionDetailResult { get; init; } =
            ServiceResult<CustomizationRequestVersionDto>.Success(new CustomizationRequestVersionDto());

        public ServiceResult<CustomizationRequestVersionDto> UpdateDraftVersionResult { get; init; } =
            ServiceResult<CustomizationRequestVersionDto>.Unauthorized();

        public ServiceResult<CreateCustomizationRequestVersionResponseDto> CreateVersionResult { get; init; } =
            ServiceResult<CreateCustomizationRequestVersionResponseDto>.Created(
                new CreateCustomizationRequestVersionResponseDto());

        public Guid ProjectId { get; private set; }
        public Guid CurrentUserId { get; private set; }
        public Guid CustomizationRequestId { get; private set; }
        public Guid CustomizationRequestVersionId { get; private set; }
        public CustomizationRequestQueryDto? Query { get; private set; }
        public CreateCustomizationRequestVersionDto? CreateVersionRequest { get; private set; }
        public UpdateCustomizationRequestVersionDto? UpdateDraftVersionRequest { get; private set; }
        public AcceptCustomizationRequestDto? AcceptVersionRequest { get; private set; }

        public Task<ServiceResult<CustomizationRequestListResponseDto>> GetByProjectAsync(
            Guid projectId, Guid currentUserId, CustomizationRequestQueryDto query, CancellationToken cancellationToken = default)
        {
            ProjectId = projectId;
            CurrentUserId = currentUserId;
            Query = query;
            return Task.FromResult(ListResult);
        }

        public Task<ServiceResult<CustomizationRequestDto>> GetDetailAsync(
            Guid customizationRequestId, Guid currentUserId, CancellationToken cancellationToken = default)
            => Task.FromResult(DetailResult);

        public Task<ServiceResult<CustomizationRequestVersionListResponseDto>> GetVersionsAsync(
            Guid customizationRequestId, Guid currentUserId, CancellationToken cancellationToken = default)
        {
            CustomizationRequestId = customizationRequestId;
            CurrentUserId = currentUserId;
            return Task.FromResult(VersionsResult);
        }

        public Task<ServiceResult<CustomizationRequestVersionDto>> GetVersionDetailAsync(
            Guid customizationRequestId,
            Guid customizationRequestVersionId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            CustomizationRequestId = customizationRequestId;
            CustomizationRequestVersionId = customizationRequestVersionId;
            CurrentUserId = currentUserId;
            return Task.FromResult(VersionDetailResult);
        }

        public Task<ServiceResult<CustomizationRequestDto>> SubmitAsync(
            Guid proposalItemId, Guid currentUserId, SubmitCustomizationRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(DetailResult);

        public Task<ServiceResult<CreateCustomizationRequestVersionResponseDto>> CreateVersionAsync(
            Guid customizationRequestId, Guid currentUserId, CreateCustomizationRequestVersionDto request, CancellationToken cancellationToken = default)
        {
            CustomizationRequestId = customizationRequestId;
            CreateVersionRequest = request;
            return Task.FromResult(CreateVersionResult);
        }

        public Task<ServiceResult<CustomizationRequestVersionDto>> UpdateDraftVersionAsync(
            Guid customizationRequestId, Guid customizationRequestVersionId, Guid currentUserId, UpdateCustomizationRequestVersionDto request, CancellationToken cancellationToken = default)
        {
            CustomizationRequestId = customizationRequestId;
            CustomizationRequestVersionId = customizationRequestVersionId;
            CurrentUserId = currentUserId;
            UpdateDraftVersionRequest = request;
            return Task.FromResult(UpdateDraftVersionResult);
        }

        public Task<ServiceResult<CustomizationRequestVersionDto>> SubmitVersionForReviewAsync(
            Guid customizationRequestId, Guid customizationRequestVersionId, Guid currentUserId, CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<CustomizationRequestVersionDto>.Unauthorized());

        public Task<ServiceResult<CustomizationRequestVersionDto>> WithdrawVersionAsync(
            Guid customizationRequestId, Guid customizationRequestVersionId, Guid currentUserId, CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<CustomizationRequestVersionDto>.Unauthorized());

        public Task<ServiceResult<CustomizationRequestDto>> AcceptVersionAsync(
            Guid customizationRequestId, Guid currentUserId, AcceptCustomizationRequestDto request, CancellationToken cancellationToken = default)
        {
            CustomizationRequestId = customizationRequestId;
            AcceptVersionRequest = request;
            return Task.FromResult(DetailResult);
        }

        public Task<ServiceResult<CustomizationRequestDto>> CancelAsync(
            Guid customizationRequestId, Guid currentUserId, CancelCustomizationRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(DetailResult);

        public Task<ServiceResult<ProductionCustomizationVersionListResponseDto>> GetProductionVersionQueueAsync(
            Guid currentUserId, ProductionCustomizationVersionQueryDto query, CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<ProductionCustomizationVersionListResponseDto>.Unauthorized());

        public Task<ServiceResult<ProductionCustomizationVersionDetailDto>> GetProductionVersionDetailAsync(
            Guid customizationRequestVersionId, Guid currentUserId, CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<ProductionCustomizationVersionDetailDto>.Unauthorized());

        public Task<ServiceResult<ProductionCustomizationVersionDetailDto>> ReviewVersionAsync(
            Guid customizationRequestVersionId, Guid currentUserId, ReviewCustomizationVersionDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<ProductionCustomizationVersionDetailDto>.Unauthorized());
    }
}
