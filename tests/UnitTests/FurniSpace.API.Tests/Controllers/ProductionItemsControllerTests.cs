#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers.Production;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Production;
using FurniSpace.Application.Interfaces.Production;
using FurniSpace.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurniSpace.API.Tests.Controllers;

public sealed class ProductionItemsControllerTests
{
    [Fact]
    public void Controller_RequiresProductionOrAdmin()
    {
        var authorize = typeof(ProductionItemsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal("PRODUCTION,ADMIN", authorize.Roles);
    }

    [Fact]
    public async Task UpdateStatus_ReturnsServiceResultAndPassesRequest()
    {
        var userId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var request = new UpdateProductionItemStatusDto
        {
            Status = ProductionItemStatus.IN_PRODUCTION,
            ProductionNote = "Started"
        };
        var service = new FakeProductionRequestService();
        var controller = BuildController(service, userId);

        var result = await controller.UpdateStatus(itemId, request);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(userId, service.CurrentUserId);
        Assert.Equal(itemId, service.ProductionItemId);
        Assert.Same(request, service.UpdateRequest);
    }

    [Fact]
    public async Task GetUnavailableItems_ReturnsServiceResultAndPassesQuery()
    {
        var userId = Guid.NewGuid();
        var response = new ProductionUnavailableItemsResponseDto
        {
            Items =
            [
                new ProductionUnavailableItemDto
                {
                    ProductionItemId = Guid.NewGuid(),
                    CancellationReason = "Out of stock"
                }
            ],
            TotalItems = 1
        };
        var service = new FakeProductionRequestService(unavailableResult: ServiceResult<ProductionUnavailableItemsResponseDto>.Success(response, "ok"));
        var controller = BuildController(service, userId);
        var query = new ProductionUnavailableItemsQueryDto { Page = 1, PageSize = 20 };

        var result = await controller.GetUnavailableItems(query);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(userId, service.CurrentUserId);
        Assert.Same(query, service.UnavailableQuery);
    }

    [Fact]
    public async Task GetUnavailableItems_WithoutUser_ReturnsUnauthorized()
    {
        var controller = BuildController(new FakeProductionRequestService(), userId: null);

        var result = await controller.GetUnavailableItems(new ProductionUnavailableItemsQueryDto());

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task UpdateStatus_WithoutUser_ReturnsUnauthorized()
    {
        var controller = BuildController(new FakeProductionRequestService(), userId: null);

        var result = await controller.UpdateStatus(Guid.NewGuid(), new UpdateProductionItemStatusDto());

        Assert.IsType<UnauthorizedResult>(result);
    }

    private static ProductionItemsController BuildController(
        IProductionRequestService service,
        Guid? userId)
    {
        var controller = new ProductionItemsController(service);
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

    private sealed class FakeProductionRequestService : IProductionRequestService
    {
        private readonly ServiceResult<ProductionUnavailableItemsResponseDto>? _unavailableResult;

        public FakeProductionRequestService(
            ServiceResult<ProductionUnavailableItemsResponseDto>? unavailableResult = null)
        {
            _unavailableResult = unavailableResult;
        }

        public Guid CurrentUserId { get; private set; }
        public Guid ProductionItemId { get; private set; }
        public UpdateProductionItemStatusDto? UpdateRequest { get; private set; }
        public ProductionUnavailableItemsQueryDto? UnavailableQuery { get; private set; }

        public Task<ServiceResult<ProductionRequestCreatedDto>> CreateAsync(
            Guid orderId,
            Guid currentUserId,
            CreateProductionRequestDto request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ServiceResult<ProductionRequestCreatedDto>.Unauthorized());
        }

        public Task<ServiceResult<List<AvailableProductionStaffDto>>> GetAvailableStaffAsync(
            Guid currentUserId,
            AvailableProductionStaffQueryDto query,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ServiceResult<List<AvailableProductionStaffDto>>.Unauthorized());
        }

        public Task<ServiceResult<ProductionRequestAssignmentDto>> AssignAsync(
            Guid productionRequestId,
            Guid currentUserId,
            AssignProductionRequestDto request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ServiceResult<ProductionRequestAssignmentDto>.Unauthorized());
        }

        public Task<ServiceResult<ProductionRequestListResponseDto>> GetQueueAsync(
            Guid currentUserId,
            ProductionRequestQueryDto query,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ServiceResult<ProductionRequestListResponseDto>.Unauthorized());
        }

        public Task<ServiceResult<ProductionRequestDetailDto>> GetDetailAsync(
            Guid productionRequestId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ServiceResult<ProductionRequestDetailDto>.Unauthorized());
        }

        public Task<ServiceResult<ProductionRequestStatusDto>> StartAsync(
            Guid productionRequestId,
            Guid currentUserId,
            StartProductionRequestDto request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ServiceResult<ProductionRequestStatusDto>.Unauthorized());
        }

        public Task<ServiceResult<ProductionItemStatusDto>> UpdateItemStatusAsync(
            Guid productionItemId,
            Guid currentUserId,
            UpdateProductionItemStatusDto request,
            CancellationToken cancellationToken = default)
        {
            CurrentUserId = currentUserId;
            ProductionItemId = productionItemId;
            UpdateRequest = request;
            return Task.FromResult(ServiceResult<ProductionItemStatusDto>.Success(
                new ProductionItemStatusDto()));
        }

        public Task<ServiceResult<ProductionUnavailableItemsResponseDto>> GetUnavailableItemsAsync(
            Guid currentUserId,
            ProductionUnavailableItemsQueryDto query,
            CancellationToken cancellationToken = default)
        {
            CurrentUserId = currentUserId;
            UnavailableQuery = query;
            return Task.FromResult(_unavailableResult
                ?? ServiceResult<ProductionUnavailableItemsResponseDto>.Success(new ProductionUnavailableItemsResponseDto()));
        }
    }
}
