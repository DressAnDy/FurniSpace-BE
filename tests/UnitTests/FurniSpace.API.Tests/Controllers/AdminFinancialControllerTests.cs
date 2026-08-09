#nullable enable

using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers.Admin;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Financial;
using FurniSpace.Application.Interfaces.Financial;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurniSpace.API.Tests.Controllers;

public sealed class AdminFinancialControllerTests
{
    [Fact]
    public async Task GetSummary_ReturnsServiceResultThroughBaseController()
    {
        var response = new AdminFinancialSummaryDto
        {
            Currency = "VND",
            CollectedAmount = 100m
        };
        var service = new FakeAdminFinancialService(
            ServiceResult<AdminFinancialSummaryDto>.Success(
                response,
                "Admin financial summary retrieved successfully."));
        var controller = new AdminFinancialController(service);
        var query = new AdminFinancialSummaryQueryDto { Period = "THIS_MONTH" };

        var actionResult = await controller.GetSummary(query);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Same(query, service.Query);
    }

    [Fact]
    public async Task GetReceivables_ReturnsServiceResultThroughBaseController()
    {
        var response = new AdminFinancialReceivablesDto
        {
            OutstandingPaymentAmount = 70m,
            ContractedReceivableAmount = 140m
        };
        var service = new FakeAdminFinancialService(
            ServiceResult<AdminFinancialSummaryDto>.Success(new AdminFinancialSummaryDto()),
            ServiceResult<AdminFinancialReceivablesDto>.Success(
                response,
                "Financial receivables retrieved successfully."));
        var controller = new AdminFinancialController(service);
        var query = new AdminFinancialReceivablesQueryDto { Page = 2, PageSize = 10 };

        var actionResult = await controller.GetReceivables(query);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Same(query, service.ReceivablesQuery);
    }

    [Fact]
    public async Task GetReceivableItems_ReturnsServiceResultThroughBaseController()
    {
        var response = new AdminFinancialReceivablesDto
        {
            Items =
            [
                new AdminFinancialReceivableItemDto
                {
                    ProjectName = "Cafe",
                    OrderCode = "ORD-001"
                }
            ]
        };
        var service = new FakeAdminFinancialService(
            ServiceResult<AdminFinancialSummaryDto>.Success(new AdminFinancialSummaryDto()),
            ServiceResult<AdminFinancialReceivablesDto>.Success(
                response,
                "Financial receivables retrieved successfully."));
        var controller = new AdminFinancialController(service);
        var query = new AdminFinancialReceivablesQueryDto { SortBy = "orderCode" };

        var actionResult = await controller.GetReceivableItems(query);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Same(query, service.ReceivablesQuery);
    }

    [Fact]
    public async Task GetPaymentBreakdown_ReturnsServiceResultThroughBaseController()
    {
        var service = new FakeAdminFinancialService(
            ServiceResult<AdminFinancialSummaryDto>.Success(new AdminFinancialSummaryDto()),
            paymentBreakdownResult: ServiceResult<AdminFinancialPaymentBreakdownDto>.Success(
                new AdminFinancialPaymentBreakdownDto { Currency = "VND" },
                "Payment breakdown retrieved successfully."));
        var controller = new AdminFinancialController(service);
        var query = new AdminFinancialPaymentBreakdownQueryDto();

        var actionResult = await controller.GetPaymentBreakdown(query);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Same(query, service.PaymentBreakdownQuery);
    }

    [Fact]
    public async Task GetCollectionTrend_ReturnsServiceResultThroughBaseController()
    {
        var service = new FakeAdminFinancialService(
            ServiceResult<AdminFinancialSummaryDto>.Success(new AdminFinancialSummaryDto()),
            collectionTrendResult: ServiceResult<AdminFinancialCollectionTrendDto>.Success(
                new AdminFinancialCollectionTrendDto { Granularity = "MONTH" },
                "Collection trend retrieved successfully."));
        var controller = new AdminFinancialController(service);
        var query = new AdminFinancialCollectionTrendQueryDto();

        var actionResult = await controller.GetCollectionTrend(query);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Same(query, service.CollectionTrendQuery);
    }

    [Fact]
    public async Task GetProjects_ReturnsServiceResultThroughBaseController()
    {
        var service = new FakeAdminFinancialService(
            ServiceResult<AdminFinancialSummaryDto>.Success(new AdminFinancialSummaryDto()),
            projectsResult: ServiceResult<AdminFinancialProjectsDto>.Success(
                new AdminFinancialProjectsDto { TotalItems = 1 },
                "Project financial overview retrieved successfully."));
        var controller = new AdminFinancialController(service);
        var query = new AdminFinancialProjectsQueryDto { Page = 2, PageSize = 10 };

        var actionResult = await controller.GetProjects(query);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Same(query, service.ProjectsQuery);
    }

    [Fact]
    public async Task GetProject_ReturnsServiceResultThroughBaseController()
    {
        var projectId = System.Guid.NewGuid();
        var service = new FakeAdminFinancialService(
            ServiceResult<AdminFinancialSummaryDto>.Success(new AdminFinancialSummaryDto()),
            projectResult: ServiceResult<AdminFinancialProjectRowDto>.Success(
                new AdminFinancialProjectRowDto { ProjectId = projectId },
                "Project financial detail retrieved successfully."));
        var controller = new AdminFinancialController(service);

        var actionResult = await controller.GetProject(projectId);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(projectId, service.ProjectId);
    }

    private sealed class FakeAdminFinancialService : IAdminFinancialService
    {
        private readonly ServiceResult<AdminFinancialSummaryDto> _result;
        private readonly ServiceResult<AdminFinancialReceivablesDto> _receivablesResult;
        private readonly ServiceResult<AdminFinancialPaymentBreakdownDto> _paymentBreakdownResult;
        private readonly ServiceResult<AdminFinancialCollectionTrendDto> _collectionTrendResult;
        private readonly ServiceResult<AdminFinancialProjectsDto> _projectsResult;
        private readonly ServiceResult<AdminFinancialProjectRowDto> _projectResult;

        public FakeAdminFinancialService(
            ServiceResult<AdminFinancialSummaryDto> result,
            ServiceResult<AdminFinancialReceivablesDto>? receivablesResult = null,
            ServiceResult<AdminFinancialPaymentBreakdownDto>? paymentBreakdownResult = null,
            ServiceResult<AdminFinancialCollectionTrendDto>? collectionTrendResult = null,
            ServiceResult<AdminFinancialProjectsDto>? projectsResult = null,
            ServiceResult<AdminFinancialProjectRowDto>? projectResult = null)
        {
            _result = result;
            _receivablesResult = receivablesResult ??
                ServiceResult<AdminFinancialReceivablesDto>.Success(new AdminFinancialReceivablesDto());
            _paymentBreakdownResult = paymentBreakdownResult ??
                ServiceResult<AdminFinancialPaymentBreakdownDto>.Success(new AdminFinancialPaymentBreakdownDto());
            _collectionTrendResult = collectionTrendResult ??
                ServiceResult<AdminFinancialCollectionTrendDto>.Success(new AdminFinancialCollectionTrendDto());
            _projectsResult = projectsResult ??
                ServiceResult<AdminFinancialProjectsDto>.Success(new AdminFinancialProjectsDto());
            _projectResult = projectResult ??
                ServiceResult<AdminFinancialProjectRowDto>.Success(new AdminFinancialProjectRowDto());
        }

        public AdminFinancialSummaryQueryDto? Query { get; private set; }
        public AdminFinancialReceivablesQueryDto? ReceivablesQuery { get; private set; }
        public AdminFinancialPaymentBreakdownQueryDto? PaymentBreakdownQuery { get; private set; }
        public AdminFinancialCollectionTrendQueryDto? CollectionTrendQuery { get; private set; }
        public AdminFinancialProjectsQueryDto? ProjectsQuery { get; private set; }
        public System.Guid? ProjectId { get; private set; }

        public Task<ServiceResult<AdminFinancialSummaryDto>> GetSummaryAsync(
            AdminFinancialSummaryQueryDto query,
            CancellationToken cancellationToken = default)
        {
            Query = query;
            return Task.FromResult(_result);
        }

        public Task<ServiceResult<AdminFinancialReceivablesDto>> GetReceivablesAsync(
            AdminFinancialReceivablesQueryDto query,
            CancellationToken cancellationToken = default)
        {
            ReceivablesQuery = query;
            return Task.FromResult(_receivablesResult);
        }

        public Task<ServiceResult<AdminFinancialPaymentBreakdownDto>> GetPaymentBreakdownAsync(
            AdminFinancialPaymentBreakdownQueryDto query,
            CancellationToken cancellationToken = default)
        {
            PaymentBreakdownQuery = query;
            return Task.FromResult(_paymentBreakdownResult);
        }

        public Task<ServiceResult<AdminFinancialCollectionTrendDto>> GetCollectionTrendAsync(
            AdminFinancialCollectionTrendQueryDto query,
            CancellationToken cancellationToken = default)
        {
            CollectionTrendQuery = query;
            return Task.FromResult(_collectionTrendResult);
        }

        public Task<ServiceResult<AdminFinancialProjectsDto>> GetProjectsAsync(
            AdminFinancialProjectsQueryDto query,
            CancellationToken cancellationToken = default)
        {
            ProjectsQuery = query;
            return Task.FromResult(_projectsResult);
        }

        public Task<ServiceResult<AdminFinancialProjectRowDto>> GetProjectAsync(
            System.Guid projectId,
            CancellationToken cancellationToken = default)
        {
            ProjectId = projectId;
            return Task.FromResult(_projectResult);
        }
    }
}
