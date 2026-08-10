#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers.Admin;
using FurniSpace.Application.Common;
using FurniSpace.Application.Interfaces.Reports;
using FurniSpace.Shared.DTOs.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurniSpace.API.Tests.Controllers;

public sealed class AdminReportsControllerTests
{
    [Fact]
    public void AdminReportsController_RequiresAdminRole()
    {
        var authorize = typeof(AdminReportsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();
        Assert.Equal("ADMIN", authorize.Roles);
    }

    [Fact]
    public void AdminProductionWorkloadController_RequiresAdminRole()
    {
        var authorize = typeof(AdminProductionWorkloadController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();
        Assert.Equal("ADMIN", authorize.Roles);
    }

    [Fact]
    public async Task GetOverview_ReturnsServiceResult()
    {
        var controller = new AdminReportsController(new FakeAdminReportService());
        var result = await controller.GetOverview(DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.IsType<ServiceResult<ReportOverviewDto>>(objectResult.Value);
    }

    [Fact]
    public async Task GetBusiness_ReturnsServiceResult()
    {
        var controller = new AdminReportsController(new FakeAdminReportService());
        var result = await controller.GetBusiness();

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
        var payload = Assert.IsType<ServiceResult<BusinessReportDto>>(objectResult.Value);
        Assert.Equal("Business report retrieved successfully.", payload.Message);
    }

    [Fact]
    public async Task GetProjects_ReturnsServiceResult()
    {
        var controller = new AdminReportsController(new FakeAdminReportService());
        var result = await controller.GetProjects(null, null);

        Assert.Equal(200, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task GetProjectAging_ReturnsServiceResult()
    {
        var controller = new AdminReportsController(new FakeAdminReportService());
        var result = await controller.GetProjectAging(new ProjectAgingQueryDto { Page = 1, PageSize = 20 });

        Assert.Equal(200, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task GetCommercial_ReturnsServiceResult()
    {
        var controller = new AdminReportsController(new FakeAdminReportService());
        var result = await controller.GetCommercial(null, null);
        Assert.Equal(200, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task GetCommercialTrend_ReturnsServiceResult()
    {
        var controller = new AdminReportsController(new FakeAdminReportService());
        var result = await controller.GetCommercialTrend(new CommercialTrendQueryDto
        {
            From = DateTime.UtcNow.AddDays(-7),
            To = DateTime.UtcNow
        });
        Assert.Equal(200, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task GetProduction_ReturnsServiceResult()
    {
        var controller = new AdminReportsController(new FakeAdminReportService());
        var result = await controller.GetProduction(null, null);
        Assert.Equal(200, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task GetDelivery_ReturnsServiceResult()
    {
        var controller = new AdminReportsController(new FakeAdminReportService());
        var result = await controller.GetDelivery(null, null);
        Assert.Equal(200, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task GetDeliveryReviews_ReturnsServiceResult()
    {
        var controller = new AdminReportsController(new FakeAdminReportService());
        var result = await controller.GetDeliveryReviews(new DeliveryReviewsQueryDto());
        Assert.Equal(200, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task GetCatalog_ReturnsServiceResult()
    {
        var controller = new AdminReportsController(new FakeAdminReportService());
        var result = await controller.GetCatalog();
        Assert.Equal(200, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task GetCatalogBestsellers_ReturnsServiceResult()
    {
        var controller = new AdminReportsController(new FakeAdminReportService());
        var result = await controller.GetCatalogBestsellers(new CatalogBestsellersQueryDto
        {
            From = DateTime.UtcNow.AddDays(-30),
            To = DateTime.UtcNow
        });
        Assert.Equal(200, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task Export_WhenSuccess_ReturnsFile()
    {
        var controller = new AdminReportsController(new FakeAdminReportService());
        var result = await controller.Export(new ReportExportQueryDto { Domain = "business" });

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("text/csv; charset=utf-8", file.ContentType);
        Assert.Equal("report-business.csv", file.FileDownloadName);
        Assert.NotEmpty(file.FileContents);
    }

    [Fact]
    public async Task Export_WhenBadRequest_ReturnsServiceResult()
    {
        var controller = new AdminReportsController(new FakeAdminReportService { ExportFails = true });
        var result = await controller.Export(new ReportExportQueryDto { Domain = "bad" });

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, objectResult.StatusCode);
    }

    [Fact]
    public async Task ProductionWorkload_ReturnsServiceResult()
    {
        var controller = new AdminProductionWorkloadController(new FakeAdminReportService());
        var result = await controller.GetWorkload(new ProductionWorkloadQueryDto { Page = 1, PageSize = 20 });

        Assert.Equal(200, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task ProductionWorkloadSummary_ReturnsServiceResult()
    {
        var controller = new AdminProductionWorkloadController(new FakeAdminReportService());
        var result = await controller.GetWorkloadSummary();

        Assert.Equal(200, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    private sealed class FakeAdminReportService : IAdminReportService
    {
        public bool ExportFails { get; set; }

        public Task<ServiceResult<ReportOverviewDto>> GetOverviewAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<ReportOverviewDto>.Success(new ReportOverviewDto(), "Report overview retrieved successfully."));

        public Task<ServiceResult<BusinessReportDto>> GetBusinessAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<BusinessReportDto>.Success(new BusinessReportDto(), "Business report retrieved successfully."));

        public Task<ServiceResult<ProjectReportDto>> GetProjectsAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<ProjectReportDto>.Success(new ProjectReportDto(), "ok"));

        public Task<ServiceResult<CommercialReportDto>> GetCommercialAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<CommercialReportDto>.Success(new CommercialReportDto(), "ok"));

        public Task<ServiceResult<ProductionReportDto>> GetProductionAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<ProductionReportDto>.Success(new ProductionReportDto(), "ok"));

        public Task<ServiceResult<DeliveryReportDto>> GetDeliveryAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<DeliveryReportDto>.Success(new DeliveryReportDto(), "ok"));

        public Task<ServiceResult<CatalogReportDto>> GetCatalogAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<CatalogReportDto>.Success(new CatalogReportDto(), "ok"));

        public Task<ServiceResult<PagedResult<ProjectAgingItemDto>>> GetProjectAgingAsync(ProjectAgingQueryDto query, CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<PagedResult<ProjectAgingItemDto>>.Success(
                PagedResult<ProjectAgingItemDto>.Create([], query.Page, query.PageSize, 0), "ok"));

        public Task<ServiceResult<CommercialTrendDto>> GetCommercialTrendAsync(CommercialTrendQueryDto query, CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<CommercialTrendDto>.Success(new CommercialTrendDto(), "ok"));

        public Task<ServiceResult<CatalogBestsellersDto>> GetCatalogBestsellersAsync(CatalogBestsellersQueryDto query, CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<CatalogBestsellersDto>.Success(new CatalogBestsellersDto(), "ok"));

        public Task<ServiceResult<DeliveryReviewsDto>> GetDeliveryReviewsAsync(DeliveryReviewsQueryDto query, CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<DeliveryReviewsDto>.Success(new DeliveryReviewsDto(), "ok"));

        public Task<ServiceResult<PagedResult<ProductionWorkloadItemDto>>> GetProductionWorkloadAsync(ProductionWorkloadQueryDto query, CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<PagedResult<ProductionWorkloadItemDto>>.Success(
                PagedResult<ProductionWorkloadItemDto>.Create([], query.Page, query.PageSize, 0), "ok"));

        public Task<ServiceResult<ProductionWorkloadSummaryDto>> GetProductionWorkloadSummaryAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<ProductionWorkloadSummaryDto>.Success(new ProductionWorkloadSummaryDto(), "ok"));

        public Task<ServiceResult<ReportExportFileDto>> ExportAsync(ReportExportQueryDto query, CancellationToken cancellationToken = default)
        {
            if (ExportFails)
            {
                return Task.FromResult(ServiceResult<ReportExportFileDto>.BadRequest("Domain is required."));
            }

            return Task.FromResult(ServiceResult<ReportExportFileDto>.Success(new ReportExportFileDto
            {
                FileName = "report-business.csv",
                ContentType = "text/csv; charset=utf-8",
                Content = [1, 2, 3]
            }, "ok"));
        }
    }
}
