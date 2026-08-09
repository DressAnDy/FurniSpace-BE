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
    [Theory]
    [InlineData(nameof(AdminReportsController.GetOverview))]
    [InlineData(nameof(AdminReportsController.GetBusiness))]
    [InlineData(nameof(AdminReportsController.GetProjects))]
    [InlineData(nameof(AdminReportsController.GetCommercial))]
    [InlineData(nameof(AdminReportsController.GetProduction))]
    [InlineData(nameof(AdminReportsController.GetDelivery))]
    [InlineData(nameof(AdminReportsController.GetCatalog))]
    [InlineData(nameof(AdminReportsController.GetProjectAging))]
    [InlineData(nameof(AdminReportsController.Export))]
    public void ReportActions_RequireAdminRole(string methodName)
    {
        var typeAuthorize = typeof(AdminReportsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();
        Assert.Equal("ADMIN", typeAuthorize.Roles);

        var method = typeof(AdminReportsController).GetMethod(methodName);
        Assert.NotNull(method);
    }

    [Fact]
    public void ProductionWorkloadActions_RequireAdminRole()
    {
        var authorize = typeof(AdminProductionWorkloadController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();
        Assert.Equal("ADMIN", authorize.Roles);
    }

    [Fact]
    public async Task GetBusiness_ReturnsServiceResult()
    {
        var controller = new AdminReportsController(new FakeAdminReportService());
        var actionResult = await controller.GetBusiness();

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var payload = Assert.IsType<ServiceResult<BusinessReportDto>>(objectResult.Value);
        Assert.Equal("Business report retrieved successfully.", payload.Message);
    }

    private sealed class FakeAdminReportService : IAdminReportService
    {
        public Task<ServiceResult<ReportOverviewDto>> GetOverviewAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
            => throw new System.NotImplementedException();

        public Task<ServiceResult<BusinessReportDto>> GetBusinessAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<BusinessReportDto>.Success(new BusinessReportDto(), "Business report retrieved successfully."));

        public Task<ServiceResult<ProjectReportDto>> GetProjectsAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
            => throw new System.NotImplementedException();

        public Task<ServiceResult<CommercialReportDto>> GetCommercialAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
            => throw new System.NotImplementedException();

        public Task<ServiceResult<ProductionReportDto>> GetProductionAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
            => throw new System.NotImplementedException();

        public Task<ServiceResult<DeliveryReportDto>> GetDeliveryAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
            => throw new System.NotImplementedException();

        public Task<ServiceResult<CatalogReportDto>> GetCatalogAsync(CancellationToken cancellationToken = default)
            => throw new System.NotImplementedException();

        public Task<ServiceResult<PagedResult<ProjectAgingItemDto>>> GetProjectAgingAsync(ProjectAgingQueryDto query, CancellationToken cancellationToken = default)
            => throw new System.NotImplementedException();

        public Task<ServiceResult<CommercialTrendDto>> GetCommercialTrendAsync(CommercialTrendQueryDto query, CancellationToken cancellationToken = default)
            => throw new System.NotImplementedException();

        public Task<ServiceResult<CatalogBestsellersDto>> GetCatalogBestsellersAsync(CatalogBestsellersQueryDto query, CancellationToken cancellationToken = default)
            => throw new System.NotImplementedException();

        public Task<ServiceResult<DeliveryReviewsDto>> GetDeliveryReviewsAsync(DeliveryReviewsQueryDto query, CancellationToken cancellationToken = default)
            => throw new System.NotImplementedException();

        public Task<ServiceResult<PagedResult<ProductionWorkloadItemDto>>> GetProductionWorkloadAsync(ProductionWorkloadQueryDto query, CancellationToken cancellationToken = default)
            => throw new System.NotImplementedException();

        public Task<ServiceResult<ProductionWorkloadSummaryDto>> GetProductionWorkloadSummaryAsync(CancellationToken cancellationToken = default)
            => throw new System.NotImplementedException();

        public Task<ServiceResult<ReportExportFileDto>> ExportAsync(ReportExportQueryDto query, CancellationToken cancellationToken = default)
            => throw new System.NotImplementedException();
    }
}
