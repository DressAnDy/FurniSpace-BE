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

public sealed class ProductionRequestsControllerTests
{
    [Fact]
    public void Controller_RequiresProductionSalesOrAdmin()
    {
        var authorize = typeof(ProductionRequestsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal("PRODUCTION,SALES,ADMIN", authorize.Roles);
    }

    [Fact]
    public void Assign_RequiresSalesOrAdmin()
    {
        var authorize = typeof(ProductionRequestsController)
            .GetMethods()
            .Single(method => method.Name == nameof(ProductionRequestsController.Assign))
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal("SALES,ADMIN", authorize.Roles);
    }

    [Theory]
    [InlineData(nameof(ProductionRequestsController.MarkFeasible))]
    [InlineData(nameof(ProductionRequestsController.Start))]
    [InlineData(nameof(ProductionRequestsController.Complete))]
    public void ProductionStatusActions_RequireProductionOrAdmin(string methodName)
    {
        var authorize = typeof(ProductionRequestsController)
            .GetMethods()
            .Single(method => method.Name == methodName)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal("PRODUCTION,ADMIN", authorize.Roles);
    }

    [Fact]
    public async Task GetQueue_ReturnsServiceResultAndPassesQuery()
    {
        var userId = Guid.NewGuid();
        var assigneeId = Guid.NewGuid();
        var service = new FakeProductionRequestService();
        var controller = BuildController(service, userId);

        var result = await controller.GetQueue(new ProductionRequestQueryDto
        {
            Status = ProductionRequestStatus.PENDING_REVIEW,
            AssignedTo = assigneeId,
            Priority = "NORMAL"
        });

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(userId, service.CurrentUserId);
        Assert.Equal(assigneeId, service.QueueQuery!.AssignedTo);
    }

    [Fact]
    public async Task GetDetail_ReturnsServiceResultAndPassesId()
    {
        var userId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var service = new FakeProductionRequestService();
        var controller = BuildController(service, userId);

        var result = await controller.GetDetail(requestId);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(requestId, service.ProductionRequestId);
        Assert.Equal(userId, service.CurrentUserId);
    }

    [Fact]
    public async Task Assign_ReturnsServiceResultAndPassesRequest()
    {
        var userId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var request = new AssignProductionRequestDto { AssignedTo = Guid.NewGuid() };
        var service = new FakeProductionRequestService();
        var controller = BuildController(service, userId);

        var result = await controller.Assign(requestId, request);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(requestId, service.ProductionRequestId);
        Assert.Same(request, service.AssignRequest);
    }

    [Fact]
    public async Task MarkFeasible_ReturnsServiceResultAndPassesRequest()
    {
        var userId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var request = new MarkProductionRequestFeasibleDto { Note = "ok" };
        var service = new FakeProductionRequestService();
        var controller = BuildController(service, userId);

        var result = await controller.MarkFeasible(requestId, request);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(requestId, service.ProductionRequestId);
        Assert.Same(request, service.MarkFeasibleRequest);
    }

    [Fact]
    public async Task Start_ReturnsServiceResultAndPassesRequest()
    {
        var userId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var request = new StartProductionRequestDto();
        var service = new FakeProductionRequestService();
        var controller = BuildController(service, userId);

        var result = await controller.Start(requestId, request);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(requestId, service.ProductionRequestId);
        Assert.Same(request, service.StartRequest);
    }

    [Fact]
    public async Task Complete_ReturnsServiceResultAndPassesId()
    {
        var userId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var service = new FakeProductionRequestService();
        var controller = BuildController(service, userId);

        var result = await controller.Complete(requestId);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(requestId, service.ProductionRequestId);
        Assert.Equal(userId, service.CurrentUserId);
    }

    [Fact]
    public async Task Actions_WithoutUser_ReturnUnauthorized()
    {
        var controller = BuildController(new FakeProductionRequestService(), userId: null);

        var queue = await controller.GetQueue(new ProductionRequestQueryDto());
        var detail = await controller.GetDetail(Guid.NewGuid());
        var assign = await controller.Assign(Guid.NewGuid(), new AssignProductionRequestDto());
        var markFeasible = await controller.MarkFeasible(Guid.NewGuid(), new MarkProductionRequestFeasibleDto());
        var start = await controller.Start(Guid.NewGuid(), new StartProductionRequestDto());
        var complete = await controller.Complete(Guid.NewGuid());

        Assert.IsType<UnauthorizedResult>(queue);
        Assert.IsType<UnauthorizedResult>(detail);
        Assert.IsType<UnauthorizedResult>(assign);
        Assert.IsType<UnauthorizedResult>(markFeasible);
        Assert.IsType<UnauthorizedResult>(start);
        Assert.IsType<UnauthorizedResult>(complete);
    }

    private static ProductionRequestsController BuildController(
        IProductionRequestService service,
        Guid? userId)
    {
        var controller = new ProductionRequestsController(service);
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
        public Guid CurrentUserId { get; private set; }
        public Guid ProductionRequestId { get; private set; }
        public ProductionRequestQueryDto? QueueQuery { get; private set; }
        public AssignProductionRequestDto? AssignRequest { get; private set; }
        public MarkProductionRequestFeasibleDto? MarkFeasibleRequest { get; private set; }
        public StartProductionRequestDto? StartRequest { get; private set; }

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
            CurrentUserId = currentUserId;
            ProductionRequestId = productionRequestId;
            AssignRequest = request;
            return Task.FromResult(ServiceResult<ProductionRequestAssignmentDto>.Success(
                new ProductionRequestAssignmentDto()));
        }

        public Task<ServiceResult<ProductionRequestListResponseDto>> GetQueueAsync(
            Guid currentUserId,
            ProductionRequestQueryDto query,
            CancellationToken cancellationToken = default)
        {
            CurrentUserId = currentUserId;
            QueueQuery = query;
            return Task.FromResult(ServiceResult<ProductionRequestListResponseDto>.Success(
                new ProductionRequestListResponseDto()));
        }

        public Task<ServiceResult<ProductionRequestDetailDto>> GetDetailAsync(
            Guid productionRequestId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            CurrentUserId = currentUserId;
            ProductionRequestId = productionRequestId;
            return Task.FromResult(ServiceResult<ProductionRequestDetailDto>.Success(
                new ProductionRequestDetailDto()));
        }

        public Task<ServiceResult<ProductionRequestStatusDto>> MarkFeasibleAsync(
            Guid productionRequestId,
            Guid currentUserId,
            MarkProductionRequestFeasibleDto request,
            CancellationToken cancellationToken = default)
        {
            CurrentUserId = currentUserId;
            ProductionRequestId = productionRequestId;
            MarkFeasibleRequest = request;
            return Task.FromResult(ServiceResult<ProductionRequestStatusDto>.Success(
                new ProductionRequestStatusDto()));
        }

        public Task<ServiceResult<ProductionRequestStatusDto>> StartAsync(
            Guid productionRequestId,
            Guid currentUserId,
            StartProductionRequestDto request,
            CancellationToken cancellationToken = default)
        {
            CurrentUserId = currentUserId;
            ProductionRequestId = productionRequestId;
            StartRequest = request;
            return Task.FromResult(ServiceResult<ProductionRequestStatusDto>.Success(
                new ProductionRequestStatusDto()));
        }

        public Task<ServiceResult<ProductionItemStatusDto>> UpdateItemStatusAsync(
            Guid productionItemId,
            Guid currentUserId,
            UpdateProductionItemStatusDto request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ServiceResult<ProductionItemStatusDto>.Unauthorized());
        }

        public Task<ServiceResult<ProductionCompletionDto>> CompleteAsync(
            Guid productionRequestId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            CurrentUserId = currentUserId;
            ProductionRequestId = productionRequestId;
            return Task.FromResult(ServiceResult<ProductionCompletionDto>.Success(
                new ProductionCompletionDto()));
        }
    }
}
