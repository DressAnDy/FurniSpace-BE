#nullable enable

using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers.Production;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.CustomizationRequests;
using FurniSpace.Application.Interfaces.CustomizationRequests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurniSpace.API.Tests.Controllers.Production;

public sealed class ProductionCustomizationVersionsControllerTests
{
    [Fact]
    public void Controller_RequiresProductionOrAdminRole()
    {
        var authorize = typeof(ProductionCustomizationVersionsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal("PRODUCTION,ADMIN", authorize.Roles);
    }

    [Fact]
    public async Task GetQueue_WithoutUser_ReturnsUnauthorized()
    {
        var controller = CreateController(new FakeCustomizationRequestService());

        var result = await controller.GetQueue(new ProductionCustomizationVersionQueryDto());

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetQueue_ReturnsServiceResult()
    {
        var userId = Guid.NewGuid();
        var service = new FakeCustomizationRequestService(
            queueResult: ServiceResult<ProductionCustomizationVersionListResponseDto>.Success(
                new ProductionCustomizationVersionListResponseDto { Items = [] },
                "ok"));
        var controller = CreateController(service, userId);

        var result = await controller.GetQueue(new ProductionCustomizationVersionQueryDto());

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
    }

    [Fact]
    public async Task GetDetail_ReturnsServiceResult()
    {
        var versionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var service = new FakeCustomizationRequestService(
            detailResult: ServiceResult<ProductionCustomizationVersionDetailDto>.Success(
                new ProductionCustomizationVersionDetailDto(),
                "ok"));
        var controller = CreateController(service, userId);

        var result = await controller.GetDetail(versionId);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(versionId, service.DetailVersionId);
    }

    [Fact]
    public async Task Review_ReturnsServiceResultAndPassesRequest()
    {
        var versionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var request = new ReviewCustomizationVersionDto { Result = "FEASIBLE", MaterialAvailable = true };
        var service = new FakeCustomizationRequestService(
            reviewResult: ServiceResult<ProductionCustomizationVersionDetailDto>.Success(
                new ProductionCustomizationVersionDetailDto(),
                "ok"));
        var controller = CreateController(service, userId);

        var result = await controller.Review(versionId, request);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(versionId, service.ReviewVersionId);
        Assert.Same(request, service.ReviewRequest);
    }

    private static ProductionCustomizationVersionsController CreateController(
        FakeCustomizationRequestService service,
        Guid? userId = null)
    {
        var controller = new ProductionCustomizationVersionsController(service);
        if (userId.HasValue)
        {
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString())
                    ], "TestAuth"))
                }
            };
        }

        return controller;
    }

    private sealed class FakeCustomizationRequestService : ICustomizationRequestService
    {
        private readonly ServiceResult<ProductionCustomizationVersionListResponseDto>? _queueResult;
        private readonly ServiceResult<ProductionCustomizationVersionDetailDto>? _detailResult;
        private readonly ServiceResult<ProductionCustomizationVersionDetailDto>? _reviewResult;

        public Guid DetailVersionId { get; private set; }
        public Guid ReviewVersionId { get; private set; }
        public ReviewCustomizationVersionDto? ReviewRequest { get; private set; }

        public FakeCustomizationRequestService(
            ServiceResult<ProductionCustomizationVersionListResponseDto>? queueResult = null,
            ServiceResult<ProductionCustomizationVersionDetailDto>? detailResult = null,
            ServiceResult<ProductionCustomizationVersionDetailDto>? reviewResult = null)
        {
            _queueResult = queueResult;
            _detailResult = detailResult;
            _reviewResult = reviewResult;
        }

        public Task<ServiceResult<ProductionCustomizationVersionListResponseDto>> GetProductionVersionQueueAsync(
            Guid currentUserId,
            ProductionCustomizationVersionQueryDto query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(
                _queueResult ?? ServiceResult<ProductionCustomizationVersionListResponseDto>.Unauthorized());

        public Task<ServiceResult<CustomizationRequestListResponseDto>> GetByProjectAsync(
            Guid projectId, Guid currentUserId, CustomizationRequestQueryDto query, CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<CustomizationRequestListResponseDto>.Unauthorized());

        public Task<ServiceResult<CustomizationRequestDto>> GetDetailAsync(
            Guid customizationRequestId, Guid currentUserId, CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<CustomizationRequestDto>.Unauthorized());

        public Task<ServiceResult<CustomizationRequestVersionListResponseDto>> GetVersionsAsync(
            Guid customizationRequestId, Guid currentUserId, CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<CustomizationRequestVersionListResponseDto>.Unauthorized());

        public Task<ServiceResult<CustomizationRequestVersionDto>> GetVersionDetailAsync(
            Guid customizationRequestId,
            Guid customizationRequestVersionId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<CustomizationRequestVersionDto>.Unauthorized());

        public Task<ServiceResult<CustomizationRequestDto>> SubmitAsync(
            Guid proposalItemId, Guid currentUserId, SubmitCustomizationRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<CustomizationRequestDto>.Unauthorized());

        public Task<ServiceResult<CreateCustomizationRequestVersionResponseDto>> CreateVersionAsync(
            Guid customizationRequestId, Guid currentUserId, CreateCustomizationRequestVersionDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<CreateCustomizationRequestVersionResponseDto>.Unauthorized());

        public Task<ServiceResult<CustomizationRequestVersionDto>> UpdateDraftVersionAsync(
            Guid customizationRequestId, Guid customizationRequestVersionId, Guid currentUserId, UpdateCustomizationRequestVersionDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<CustomizationRequestVersionDto>.Unauthorized());

        public Task<ServiceResult<CustomizationRequestVersionDto>> SubmitVersionForReviewAsync(
            Guid customizationRequestId, Guid customizationRequestVersionId, Guid currentUserId, CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<CustomizationRequestVersionDto>.Unauthorized());

        public Task<ServiceResult<CustomizationRequestVersionDto>> WithdrawVersionAsync(
            Guid customizationRequestId, Guid customizationRequestVersionId, Guid currentUserId, CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<CustomizationRequestVersionDto>.Unauthorized());

        public Task<ServiceResult<CustomizationRequestDto>> AcceptVersionAsync(
            Guid customizationRequestId, Guid currentUserId, AcceptCustomizationRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<CustomizationRequestDto>.Unauthorized());

        public Task<ServiceResult<CustomizationRequestDto>> CancelAsync(
            Guid customizationRequestId, Guid currentUserId, CancelCustomizationRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<CustomizationRequestDto>.Unauthorized());

        public Task<ServiceResult<ProductionCustomizationVersionDetailDto>> GetProductionVersionDetailAsync(
            Guid customizationRequestVersionId, Guid currentUserId, CancellationToken cancellationToken = default)
        {
            DetailVersionId = customizationRequestVersionId;
            return Task.FromResult(
                _detailResult ?? ServiceResult<ProductionCustomizationVersionDetailDto>.Unauthorized());
        }

        public Task<ServiceResult<ProductionCustomizationVersionDetailDto>> ReviewVersionAsync(
            Guid customizationRequestVersionId, Guid currentUserId, ReviewCustomizationVersionDto request, CancellationToken cancellationToken = default)
        {
            ReviewVersionId = customizationRequestVersionId;
            ReviewRequest = request;
            return Task.FromResult(
                _reviewResult ?? ServiceResult<ProductionCustomizationVersionDetailDto>.Unauthorized());
        }
    }
}
