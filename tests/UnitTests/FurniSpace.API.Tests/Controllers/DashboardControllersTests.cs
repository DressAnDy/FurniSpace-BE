#nullable enable

using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers.Dashboard;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Dashboard;
using FurniSpace.Application.Interfaces.Dashboard;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurniSpace.API.Tests.Controllers;

public sealed class DashboardControllersTests
{
    [Fact]
    public async Task Sales_GetActionQueue_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        var service = new FakeDashboardQueueService();
        var controller = CreateSalesController(service, userId);

        var result = await controller.GetActionQueue(new DashboardQueueQueryDto());

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(userId, service.LastUserId);
        Assert.Equal("sales-queue", service.LastCall);
    }

    [Fact]
    public async Task Sales_GetActionQueue_WithoutUser_ReturnsUnauthorized()
    {
        var controller = CreateSalesController(new FakeDashboardQueueService(), userId: null);

        var result = await controller.GetActionQueue(new DashboardQueueQueryDto());

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Sales_GetKpis_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        var service = new FakeDashboardQueueService();
        var controller = CreateSalesController(service, userId);

        var result = await controller.GetKpis(new DashboardQueueQueryDto { Scope = "mine" });

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal("sales-kpis", service.LastCall);
    }

    [Fact]
    public async Task Sales_GetKpis_WithoutUser_ReturnsUnauthorized()
    {
        var controller = CreateSalesController(new FakeDashboardQueueService(), userId: null);

        var result = await controller.GetKpis(new DashboardQueueQueryDto());

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Designer_GetWorkQueue_AndKpis_ReturnOk()
    {
        var userId = Guid.NewGuid();
        var service = new FakeDashboardQueueService();
        var controller = CreateDesignerController(service, userId);

        var queueResult = await controller.GetWorkQueue(new DashboardQueueQueryDto());
        var kpiResult = await controller.GetKpis(new DashboardQueueQueryDto());

        Assert.Equal(200, Assert.IsType<ObjectResult>(queueResult).StatusCode);
        Assert.Equal(200, Assert.IsType<ObjectResult>(kpiResult).StatusCode);
        Assert.Equal("designer-kpis", service.LastCall);
    }

    [Fact]
    public async Task Designer_WithoutUser_ReturnsUnauthorized()
    {
        var controller = CreateDesignerController(new FakeDashboardQueueService(), userId: null);

        Assert.IsType<UnauthorizedResult>(await controller.GetWorkQueue(new DashboardQueueQueryDto()));
        Assert.IsType<UnauthorizedResult>(await controller.GetKpis(new DashboardQueueQueryDto()));
    }

    [Fact]
    public async Task Production_GetQueue_AndKpis_ReturnOk()
    {
        var userId = Guid.NewGuid();
        var service = new FakeDashboardQueueService();
        var controller = CreateProductionController(service, userId);

        var queueResult = await controller.GetQueue(new DashboardQueueQueryDto());
        var kpiResult = await controller.GetKpis(new DashboardQueueQueryDto());

        Assert.Equal(200, Assert.IsType<ObjectResult>(queueResult).StatusCode);
        Assert.Equal(200, Assert.IsType<ObjectResult>(kpiResult).StatusCode);
        Assert.Equal("production-kpis", service.LastCall);
    }

    [Fact]
    public async Task Production_WithoutUser_ReturnsUnauthorized()
    {
        var controller = CreateProductionController(new FakeDashboardQueueService(), userId: null);

        Assert.IsType<UnauthorizedResult>(await controller.GetQueue(new DashboardQueueQueryDto()));
        Assert.IsType<UnauthorizedResult>(await controller.GetKpis(new DashboardQueueQueryDto()));
    }

    private static SalesDashboardController CreateSalesController(FakeDashboardQueueService service, Guid? userId)
    {
        return new SalesDashboardController(service)
        {
            ControllerContext = BuildContext(userId)
        };
    }

    private static DesignerDashboardController CreateDesignerController(
        FakeDashboardQueueService service,
        Guid? userId)
    {
        return new DesignerDashboardController(service)
        {
            ControllerContext = BuildContext(userId)
        };
    }

    private static ProductionDashboardController CreateProductionController(
        FakeDashboardQueueService service,
        Guid? userId)
    {
        return new ProductionDashboardController(service)
        {
            ControllerContext = BuildContext(userId)
        };
    }

    private static ControllerContext BuildContext(Guid? userId)
    {
        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = BuildUser(userId)
            }
        };
    }

    private static ClaimsPrincipal BuildUser(Guid? userId)
    {
        if (!userId.HasValue)
        {
            return new ClaimsPrincipal(new ClaimsIdentity());
        }

        return new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString())
        ], "Test"));
    }

    private sealed class FakeDashboardQueueService : IDashboardQueueService
    {
        public Guid LastUserId { get; private set; }
        public string? LastCall { get; private set; }

        public Task<ServiceResult<DashboardQueueResponseDto>> GetSalesActionQueueAsync(
            Guid currentUserId,
            DashboardQueueQueryDto query,
            CancellationToken cancellationToken = default)
        {
            LastUserId = currentUserId;
            LastCall = "sales-queue";
            return Task.FromResult(ServiceResult<DashboardQueueResponseDto>.Success(
                new DashboardQueueResponseDto(),
                "ok"));
        }

        public Task<ServiceResult<SalesDashboardKpisDto>> GetSalesKpisAsync(
            Guid currentUserId,
            DashboardQueueQueryDto query,
            CancellationToken cancellationToken = default)
        {
            LastUserId = currentUserId;
            LastCall = "sales-kpis";
            return Task.FromResult(ServiceResult<SalesDashboardKpisDto>.Success(
                new SalesDashboardKpisDto { NewRequests = 1 },
                "ok"));
        }

        public Task<ServiceResult<DashboardQueueResponseDto>> GetDesignerWorkQueueAsync(
            Guid currentUserId,
            DashboardQueueQueryDto query,
            CancellationToken cancellationToken = default)
        {
            LastUserId = currentUserId;
            LastCall = "designer-queue";
            return Task.FromResult(ServiceResult<DashboardQueueResponseDto>.Success(
                new DashboardQueueResponseDto(),
                "ok"));
        }

        public Task<ServiceResult<DesignerDashboardKpisDto>> GetDesignerKpisAsync(
            Guid currentUserId,
            DashboardQueueQueryDto query,
            CancellationToken cancellationToken = default)
        {
            LastUserId = currentUserId;
            LastCall = "designer-kpis";
            return Task.FromResult(ServiceResult<DesignerDashboardKpisDto>.Success(
                new DesignerDashboardKpisDto { MeasurementDue = 2 },
                "ok"));
        }

        public Task<ServiceResult<DashboardQueueResponseDto>> GetProductionQueueAsync(
            Guid currentUserId,
            DashboardQueueQueryDto query,
            CancellationToken cancellationToken = default)
        {
            LastUserId = currentUserId;
            LastCall = "production-queue";
            return Task.FromResult(ServiceResult<DashboardQueueResponseDto>.Success(
                new DashboardQueueResponseDto(),
                "ok"));
        }

        public Task<ServiceResult<ProductionDashboardKpisDto>> GetProductionKpisAsync(
            Guid currentUserId,
            DashboardQueueQueryDto query,
            CancellationToken cancellationToken = default)
        {
            LastUserId = currentUserId;
            LastCall = "production-kpis";
            return Task.FromResult(ServiceResult<ProductionDashboardKpisDto>.Success(
                new ProductionDashboardKpisDto { PendingReview = 3 },
                "ok"));
        }
    }
}
