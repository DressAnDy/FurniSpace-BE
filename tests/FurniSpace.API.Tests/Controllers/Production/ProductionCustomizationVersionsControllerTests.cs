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

        public FakeCustomizationRequestService(
            ServiceResult<ProductionCustomizationVersionListResponseDto>? queueResult = null)
        {
            _queueResult = queueResult;
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

        public Task<ServiceResult<CustomizationRequestDetailDto>> GetDetailAsync(
            Guid customizationRequestId, Guid currentUserId, CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<CustomizationRequestDetailDto>.Unauthorized());

        public Task<ServiceResult<CustomizationRequestVersionListResponseDto>> GetVersionsAsync(
            Guid customizationRequestId, Guid currentUserId, CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<CustomizationRequestVersionListResponseDto>.Unauthorized());

        public Task<ServiceResult<CustomizationRequestVersionDto>> GetVersionDetailAsync(
            Guid customizationRequestId,
            Guid customizationRequestVersionId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<CustomizationRequestVersionDto>.Unauthorized());

        public Task<ServiceResult<CustomizationRequestDetailDto>> SubmitAsync(
            Guid proposalItemId, Guid currentUserId, SubmitCustomizationRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<CustomizationRequestDetailDto>.Unauthorized());

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

        public Task<ServiceResult<CustomizationRequestDetailDto>> AcceptVersionAsync(
            Guid customizationRequestId, Guid currentUserId, AcceptCustomizationRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<CustomizationRequestDetailDto>.Unauthorized());

        public Task<ServiceResult<CustomizationRequestDetailDto>> CancelAsync(
            Guid customizationRequestId, Guid currentUserId, CancelCustomizationRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<CustomizationRequestDetailDto>.Unauthorized());

        public Task<ServiceResult<ProductionCustomizationVersionDetailDto>> GetProductionVersionDetailAsync(
            Guid customizationRequestVersionId, Guid currentUserId, CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<ProductionCustomizationVersionDetailDto>.Unauthorized());

        public Task<ServiceResult<ProductionCustomizationVersionDetailDto>> ReviewVersionAsync(
            Guid customizationRequestVersionId, Guid currentUserId, ReviewCustomizationVersionDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<ProductionCustomizationVersionDetailDto>.Unauthorized());
    }
}
