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

public sealed class ProductionCustomizationRequestsControllerTests
{
    [Fact]
    public void Controller_RequiresProductionOrAdminRole()
    {
        var authorize = typeof(ProductionCustomizationRequestsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal("PRODUCTION,ADMIN", authorize.Roles);
    }

    [Fact]
    public async Task GetList_WithoutUser_ReturnsUnauthorized()
    {
        var controller = CreateController(new FakeCustomizationRequestService());

        var result = await controller.GetList(new ProductionCustomizationRequestQueryDto());

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetList_ReturnsServiceResult()
    {
        var userId = Guid.NewGuid();
        var service = new FakeCustomizationRequestService(
            queueResult: ServiceResult<ProductionCustomizationRequestListResponseDto>.Success(
                new ProductionCustomizationRequestListResponseDto { Items = [] },
                "ok"));
        var controller = CreateController(service, userId);

        var result = await controller.GetList(new ProductionCustomizationRequestQueryDto());

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
    }

    private static ProductionCustomizationRequestsController CreateController(
        FakeCustomizationRequestService service,
        Guid? userId = null)
    {
        var controller = new ProductionCustomizationRequestsController(service);
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
        private readonly ServiceResult<ProductionCustomizationRequestListResponseDto>? _queueResult;

        public FakeCustomizationRequestService(
            ServiceResult<ProductionCustomizationRequestListResponseDto>? queueResult = null)
        {
            _queueResult = queueResult;
        }

        public Task<ServiceResult<ProductionCustomizationRequestListResponseDto>> GetProductionQueueAsync(
            Guid currentUserId,
            ProductionCustomizationRequestQueryDto query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(
                _queueResult ?? ServiceResult<ProductionCustomizationRequestListResponseDto>.Unauthorized());

        public Task<ServiceResult<CustomizationRequestListResponseDto>> GetByProjectAsync(
            Guid projectId,
            Guid currentUserId,
            CustomizationRequestQueryDto query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<CustomizationRequestListResponseDto>.Unauthorized());

        public Task<ServiceResult<CustomizationRequestDetailDto>> GetDetailAsync(
            Guid customizationRequestId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<CustomizationRequestDetailDto>.Unauthorized());

        public Task<ServiceResult<CustomizationRequestDetailDto>> SubmitAsync(
            Guid proposalItemId,
            Guid currentUserId,
            SubmitCustomizationRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<CustomizationRequestDetailDto>.Unauthorized());

        public Task<ServiceResult<CustomizationRequestDetailDto>> DesignerReviewAsync(
            Guid customizationRequestId,
            Guid currentUserId,
            DesignerReviewCustomizationRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<CustomizationRequestDetailDto>.Unauthorized());

        public Task<ServiceResult<CustomizationRequestDetailDto>> ProductionReviewAsync(
            Guid customizationRequestId,
            Guid currentUserId,
            ProductionReviewCustomizationRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<CustomizationRequestDetailDto>.Unauthorized());

        public Task<ServiceResult<CustomizationRequestDetailDto>> CustomerDecisionAsync(
            Guid customizationRequestId,
            Guid currentUserId,
            CustomerDecisionCustomizationRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<CustomizationRequestDetailDto>.Unauthorized());

        public Task<ServiceResult<CreateCustomizationProductVersionResponseDto>> CreateCustomizationProductVersionAsync(
            Guid customizationRequestId,
            Guid currentUserId,
            CreateCustomizationProductVersionRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<CreateCustomizationProductVersionResponseDto>.Unauthorized());

        public Task<ServiceResult<CustomizationRequestDetailDto>> CancelAsync(
            Guid customizationRequestId,
            Guid currentUserId,
            CancelCustomizationRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<CustomizationRequestDetailDto>.Unauthorized());
    }
}
