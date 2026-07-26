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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurniSpace.API.Tests.Controllers;

public sealed class ProductionStaffControllerTests
{
    [Fact]
    public void Controller_RequiresSalesOrAdmin()
    {
        var authorize = typeof(ProductionStaffController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal("SALES,ADMIN", authorize.Roles);
    }

    [Fact]
    public async Task GetAvailable_ReturnsServiceResultAndPassesQuery()
    {
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var service = new FakeProductionRequestService(
            ServiceResult<List<AvailableProductionStaffDto>>.Success([], "ok"));
        var controller = BuildController(service, userId);

        var result = await controller.GetAvailable(new AvailableProductionStaffQueryDto
        {
            ProjectId = projectId,
            Search = "prod"
        });

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(userId, service.CurrentUserId);
        Assert.Equal(projectId, service.Query!.ProjectId);
        Assert.Equal("prod", service.Query.Search);
    }

    [Fact]
    public async Task GetAvailable_WithoutUser_ReturnsUnauthorized()
    {
        var controller = BuildController(new FakeProductionRequestService(), userId: null);

        var result = await controller.GetAvailable(new AvailableProductionStaffQueryDto());

        Assert.IsType<UnauthorizedResult>(result);
    }

    private static ProductionStaffController BuildController(
        IProductionRequestService service,
        Guid? userId)
    {
        var controller = new ProductionStaffController(service);
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
        private readonly ServiceResult<List<AvailableProductionStaffDto>>? _availableResult;

        public FakeProductionRequestService(
            ServiceResult<List<AvailableProductionStaffDto>>? availableResult = null)
        {
            _availableResult = availableResult;
        }

        public Guid CurrentUserId { get; private set; }
        public AvailableProductionStaffQueryDto? Query { get; private set; }

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
            CurrentUserId = currentUserId;
            Query = query;
            return Task.FromResult(
                _availableResult ?? ServiceResult<List<AvailableProductionStaffDto>>.Unauthorized());
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

        public Task<ServiceResult<ProductionRequestStatusDto>> MarkFeasibleAsync(
            Guid productionRequestId,
            Guid currentUserId,
            MarkProductionRequestFeasibleDto request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ServiceResult<ProductionRequestStatusDto>.Unauthorized());
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
            return Task.FromResult(ServiceResult<ProductionItemStatusDto>.Unauthorized());
        }
    }
}
