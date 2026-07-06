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
    [InlineData(nameof(CustomizationRequestsController.Submit), "CUSTOMER")]
    [InlineData(nameof(CustomizationRequestsController.DesignerReview), "DESIGNER,ADMIN")]
    [InlineData(nameof(CustomizationRequestsController.ProductionReview), "PRODUCTION,ADMIN")]
    [InlineData(nameof(CustomizationRequestsController.CustomerDecision), "CUSTOMER")]
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
        var proposalItemId = Guid.NewGuid();
        var service = new FakeCustomizationRequestService();
        var controller = BuildController(service, userId);

        var actionResult = await controller.GetByProject(
            projectId,
            proposalId,
            proposalItemId,
            CustomizationStatus.SUBMITTED);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(projectId, service.ProjectId);
        Assert.Equal(userId, service.CurrentUserId);
        Assert.Equal(proposalId, service.Query!.ProposalId);
        Assert.Equal(proposalItemId, service.Query.ProposalItemId);
        Assert.Equal(CustomizationStatus.SUBMITTED, service.Query.Status);
    }

    [Fact]
    public async Task GetDetail_ReturnsServiceResultAndPassesId()
    {
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var service = new FakeCustomizationRequestService();
        var controller = BuildController(service, userId);

        var actionResult = await controller.GetDetail(requestId);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(requestId, service.CustomizationRequestId);
        Assert.Equal(userId, service.CurrentUserId);
    }

    [Fact]
    public async Task Submit_ReturnsCreatedResultAndPassesRequest()
    {
        var proposalItemId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var request = new SubmitCustomizationRequestDto { RequestTitle = "Change material" };
        var service = new FakeCustomizationRequestService
        {
            SubmitResult = ServiceResult<CustomizationRequestDetailDto>.Created(new CustomizationRequestDetailDto())
        };
        var controller = BuildController(service, userId);

        var actionResult = await controller.Submit(proposalItemId, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(201, objectResult.StatusCode);
        Assert.Equal(proposalItemId, service.ProposalItemId);
        Assert.Same(request, service.SubmitRequest);
    }

    [Fact]
    public async Task DesignerReview_ReturnsServiceResultAndPassesRequest()
    {
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var request = new DesignerReviewCustomizationRequestDto { DesignerSpecNote = "Possible" };
        var service = new FakeCustomizationRequestService();
        var controller = BuildController(service, userId);

        var actionResult = await controller.DesignerReview(requestId, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(requestId, service.CustomizationRequestId);
        Assert.Equal(userId, service.CurrentUserId);
        Assert.Same(request, service.DesignerReviewRequest);
    }

    [Fact]
    public async Task ProductionReview_ReturnsServiceResultAndPassesRequest()
    {
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var request = new ProductionReviewCustomizationRequestDto { Result = "FEASIBLE" };
        var service = new FakeCustomizationRequestService();
        var controller = BuildController(service, userId);

        var actionResult = await controller.ProductionReview(requestId, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(requestId, service.CustomizationRequestId);
        Assert.Equal(userId, service.CurrentUserId);
        Assert.Same(request, service.ProductionReviewRequest);
    }

    [Fact]
    public async Task CustomerDecision_ReturnsServiceResultAndPassesRequest()
    {
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var request = new CustomerDecisionCustomizationRequestDto { Decision = "ACCEPT" };
        var service = new FakeCustomizationRequestService();
        var controller = BuildController(service, userId);

        var actionResult = await controller.CustomerDecision(requestId, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(requestId, service.CustomizationRequestId);
        Assert.Equal(userId, service.CurrentUserId);
        Assert.Same(request, service.CustomerDecisionRequest);
    }

    [Fact]
    public async Task GetByProject_WithoutUserClaim_ReturnsUnauthorized()
    {
        var controller = BuildController(new FakeCustomizationRequestService(), userId: null);

        var actionResult = await controller.GetByProject(Guid.NewGuid());

        Assert.IsType<UnauthorizedResult>(actionResult);
    }

    [Fact]
    public async Task DesignerReview_WithoutUserClaim_ReturnsUnauthorized()
    {
        var controller = BuildController(new FakeCustomizationRequestService(), userId: null);

        var actionResult = await controller.DesignerReview(
            Guid.NewGuid(),
            new DesignerReviewCustomizationRequestDto());

        Assert.IsType<UnauthorizedResult>(actionResult);
    }

    [Fact]
    public async Task ProductionReview_WithoutUserClaim_ReturnsUnauthorized()
    {
        var controller = BuildController(new FakeCustomizationRequestService(), userId: null);

        var actionResult = await controller.ProductionReview(
            Guid.NewGuid(),
            new ProductionReviewCustomizationRequestDto());

        Assert.IsType<UnauthorizedResult>(actionResult);
    }

    [Fact]
    public async Task CustomerDecision_WithoutUserClaim_ReturnsUnauthorized()
    {
        var controller = BuildController(new FakeCustomizationRequestService(), userId: null);

        var actionResult = await controller.CustomerDecision(
            Guid.NewGuid(),
            new CustomerDecisionCustomizationRequestDto());

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

        public ServiceResult<CustomizationRequestDetailDto> DetailResult { get; init; } =
            ServiceResult<CustomizationRequestDetailDto>.Success(new CustomizationRequestDetailDto());

        public ServiceResult<CustomizationRequestDetailDto> SubmitResult { get; init; } =
            ServiceResult<CustomizationRequestDetailDto>.Success(new CustomizationRequestDetailDto());

        public Guid ProjectId { get; private set; }
        public Guid CurrentUserId { get; private set; }
        public Guid CustomizationRequestId { get; private set; }
        public Guid ProposalItemId { get; private set; }
        public CustomizationRequestQueryDto? Query { get; private set; }
        public SubmitCustomizationRequestDto? SubmitRequest { get; private set; }
        public DesignerReviewCustomizationRequestDto? DesignerReviewRequest { get; private set; }
        public ProductionReviewCustomizationRequestDto? ProductionReviewRequest { get; private set; }
        public CustomerDecisionCustomizationRequestDto? CustomerDecisionRequest { get; private set; }

        public Task<ServiceResult<CustomizationRequestListResponseDto>> GetByProjectAsync(
            Guid projectId,
            Guid currentUserId,
            CustomizationRequestQueryDto query,
            CancellationToken cancellationToken = default)
        {
            ProjectId = projectId;
            CurrentUserId = currentUserId;
            Query = query;
            return Task.FromResult(ListResult);
        }

        public Task<ServiceResult<CustomizationRequestDetailDto>> GetDetailAsync(
            Guid customizationRequestId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            CustomizationRequestId = customizationRequestId;
            CurrentUserId = currentUserId;
            return Task.FromResult(DetailResult);
        }

        public Task<ServiceResult<CustomizationRequestDetailDto>> SubmitAsync(
            Guid proposalItemId,
            Guid currentUserId,
            SubmitCustomizationRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ProposalItemId = proposalItemId;
            CurrentUserId = currentUserId;
            SubmitRequest = request;
            return Task.FromResult(SubmitResult);
        }

        public Task<ServiceResult<CustomizationRequestDetailDto>> DesignerReviewAsync(
            Guid customizationRequestId,
            Guid currentUserId,
            DesignerReviewCustomizationRequestDto request,
            CancellationToken cancellationToken = default)
        {
            CustomizationRequestId = customizationRequestId;
            CurrentUserId = currentUserId;
            DesignerReviewRequest = request;
            return Task.FromResult(DetailResult);
        }

        public Task<ServiceResult<CustomizationRequestDetailDto>> ProductionReviewAsync(
            Guid customizationRequestId,
            Guid currentUserId,
            ProductionReviewCustomizationRequestDto request,
            CancellationToken cancellationToken = default)
        {
            CustomizationRequestId = customizationRequestId;
            CurrentUserId = currentUserId;
            ProductionReviewRequest = request;
            return Task.FromResult(DetailResult);
        }

        public Task<ServiceResult<CustomizationRequestDetailDto>> CustomerDecisionAsync(
            Guid customizationRequestId,
            Guid currentUserId,
            CustomerDecisionCustomizationRequestDto request,
            CancellationToken cancellationToken = default)
        {
            CustomizationRequestId = customizationRequestId;
            CurrentUserId = currentUserId;
            CustomerDecisionRequest = request;
            return Task.FromResult(DetailResult);
        }
    }
}
