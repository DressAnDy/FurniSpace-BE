#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers.Admin;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Financial;
using FurniSpace.Application.Interfaces.Financial;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurniSpace.API.Tests.Controllers;

public sealed class AdminFinancialDiscountControllerTests
{
    [Fact]
    public async Task GetSummary_ReturnsServiceResultThroughBaseController()
    {
        var response = new AdminFinancialDiscountSummaryDto { TotalDiscountAmount = 70m };
        var service = new FakeAdminFinancialDiscountService(
            summaryResult: ServiceResult<AdminFinancialDiscountSummaryDto>.Success(response, "ok"));
        var controller = new AdminFinancialDiscountController(service);
        var query = new AdminFinancialDiscountSummaryQueryDto
        {
            From = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.FromHours(7)),
            To = new DateTimeOffset(2026, 8, 31, 0, 0, 0, TimeSpan.FromHours(7))
        };

        var actionResult = await controller.GetSummary(query);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Same(query, service.SummaryQuery);
    }

    [Fact]
    public async Task GetProjects_ReturnsServiceResultThroughBaseController()
    {
        var service = new FakeAdminFinancialDiscountService(
            projectsResult: ServiceResult<AdminFinancialDiscountProjectsDto>.Success(
                new AdminFinancialDiscountProjectsDto { TotalItems = 2 },
                "ok"));
        var controller = new AdminFinancialDiscountController(service);
        var query = new AdminFinancialDiscountProjectsQueryDto { Page = 1, PageSize = 20 };

        var actionResult = await controller.GetProjects(query);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Same(query, service.ProjectsQuery);
    }

    [Fact]
    public async Task GetOrderDetail_ReturnsServiceResultThroughBaseController()
    {
        var orderId = Guid.NewGuid();
        var service = new FakeAdminFinancialDiscountService(
            orderDetailResult: ServiceResult<AdminFinancialDiscountOrderDetailDto>.Success(
                new AdminFinancialDiscountOrderDetailDto { OrderId = orderId },
                "ok"));
        var controller = new AdminFinancialDiscountController(service);

        var actionResult = await controller.GetOrderDetail(orderId);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(orderId, service.OrderId);
    }

    [Fact]
    public async Task GetTrend_ReturnsServiceResultThroughBaseController()
    {
        var service = new FakeAdminFinancialDiscountService(
            trendResult: ServiceResult<AdminFinancialDiscountTrendDto>.Success(
                new AdminFinancialDiscountTrendDto { Granularity = "MONTH" },
                "ok"));
        var controller = new AdminFinancialDiscountController(service);
        var query = new AdminFinancialDiscountTrendQueryDto { Granularity = "MONTH" };

        var actionResult = await controller.GetTrend(query);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Same(query, service.TrendQuery);
    }

    [Fact]
    public async Task GetExceptions_ReturnsServiceResultThroughBaseController()
    {
        var service = new FakeAdminFinancialDiscountService(
            exceptionsResult: ServiceResult<AdminFinancialDiscountExceptionsDto>.Success(
                new AdminFinancialDiscountExceptionsDto { TotalItems = 1 },
                "ok"));
        var controller = new AdminFinancialDiscountController(service);
        var query = new AdminFinancialDiscountExceptionsQueryDto { ThresholdRate = 20m };

        var actionResult = await controller.GetExceptions(query);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Same(query, service.ExceptionsQuery);
    }

    private sealed class FakeAdminFinancialDiscountService : IAdminFinancialDiscountService
    {
        private readonly ServiceResult<AdminFinancialDiscountSummaryDto> _summaryResult;
        private readonly ServiceResult<AdminFinancialDiscountProjectsDto> _projectsResult;
        private readonly ServiceResult<AdminFinancialDiscountOrderDetailDto> _orderDetailResult;
        private readonly ServiceResult<AdminFinancialDiscountTrendDto> _trendResult;
        private readonly ServiceResult<AdminFinancialDiscountExceptionsDto> _exceptionsResult;

        public FakeAdminFinancialDiscountService(
            ServiceResult<AdminFinancialDiscountSummaryDto>? summaryResult = null,
            ServiceResult<AdminFinancialDiscountProjectsDto>? projectsResult = null,
            ServiceResult<AdminFinancialDiscountOrderDetailDto>? orderDetailResult = null,
            ServiceResult<AdminFinancialDiscountTrendDto>? trendResult = null,
            ServiceResult<AdminFinancialDiscountExceptionsDto>? exceptionsResult = null)
        {
            _summaryResult = summaryResult
                ?? ServiceResult<AdminFinancialDiscountSummaryDto>.Success(new AdminFinancialDiscountSummaryDto());
            _projectsResult = projectsResult
                ?? ServiceResult<AdminFinancialDiscountProjectsDto>.Success(new AdminFinancialDiscountProjectsDto());
            _orderDetailResult = orderDetailResult
                ?? ServiceResult<AdminFinancialDiscountOrderDetailDto>.Success(new AdminFinancialDiscountOrderDetailDto());
            _trendResult = trendResult
                ?? ServiceResult<AdminFinancialDiscountTrendDto>.Success(new AdminFinancialDiscountTrendDto());
            _exceptionsResult = exceptionsResult
                ?? ServiceResult<AdminFinancialDiscountExceptionsDto>.Success(new AdminFinancialDiscountExceptionsDto());
        }

        public AdminFinancialDiscountSummaryQueryDto? SummaryQuery { get; private set; }
        public AdminFinancialDiscountProjectsQueryDto? ProjectsQuery { get; private set; }
        public Guid OrderId { get; private set; }
        public AdminFinancialDiscountTrendQueryDto? TrendQuery { get; private set; }
        public AdminFinancialDiscountExceptionsQueryDto? ExceptionsQuery { get; private set; }

        public Task<ServiceResult<AdminFinancialDiscountSummaryDto>> GetSummaryAsync(
            AdminFinancialDiscountSummaryQueryDto query,
            CancellationToken cancellationToken = default)
        {
            SummaryQuery = query;
            return Task.FromResult(_summaryResult);
        }

        public Task<ServiceResult<AdminFinancialDiscountProjectsDto>> GetProjectsAsync(
            AdminFinancialDiscountProjectsQueryDto query,
            CancellationToken cancellationToken = default)
        {
            ProjectsQuery = query;
            return Task.FromResult(_projectsResult);
        }

        public Task<ServiceResult<AdminFinancialDiscountOrderDetailDto>> GetOrderDetailAsync(
            Guid orderId,
            CancellationToken cancellationToken = default)
        {
            OrderId = orderId;
            return Task.FromResult(_orderDetailResult);
        }

        public Task<ServiceResult<AdminFinancialDiscountTrendDto>> GetTrendAsync(
            AdminFinancialDiscountTrendQueryDto query,
            CancellationToken cancellationToken = default)
        {
            TrendQuery = query;
            return Task.FromResult(_trendResult);
        }

        public Task<ServiceResult<AdminFinancialDiscountExceptionsDto>> GetExceptionsAsync(
            AdminFinancialDiscountExceptionsQueryDto query,
            CancellationToken cancellationToken = default)
        {
            ExceptionsQuery = query;
            return Task.FromResult(_exceptionsResult);
        }
    }
}
