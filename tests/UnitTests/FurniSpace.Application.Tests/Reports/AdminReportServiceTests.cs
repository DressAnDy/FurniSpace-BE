#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Accounts;
using FurniSpace.Application.Interfaces.Accounts;
using FurniSpace.Application.Services.Reports;
using FurniSpace.Infrastructure.Repositories.IRepository;
using FurniSpace.Shared.DTOs.Reports;
using Xunit;

namespace FurniSpace.Application.Tests.Reports;

public sealed class AdminReportServiceTests
{
    [Fact]
    public async Task GetOverviewAsync_InvalidDateRange_ReturnsBadRequest()
    {
        var service = CreateService();
        var result = await service.GetOverviewAsync(
            DateTime.UtcNow.Date.AddDays(1),
            DateTime.UtcNow.Date);

        Assert.Equal(400, result.Status);
        Assert.Equal("From date must be less than or equal to To date.", result.Message);
    }

    [Fact]
    public async Task GetBusinessAsync_ComposesWorkloadSummaries()
    {
        var service = CreateService();
        var result = await service.GetBusinessAsync();

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(5, result.Data.Designer.TotalActiveDesigners);
        Assert.Equal(4, result.Data.Sales.TotalActiveSales);
        Assert.Equal(3, result.Data.Sales.UnassignedIntakeCount);
        Assert.Contains(result.Data.AccountsByRole, item => item.Key == "SALES" && item.Count == 4);
        Assert.Contains(result.Data.AccountsByStatus, item => item.Key == "ACTIVE" && item.Count == 40);
    }

    [Fact]
    public async Task GetOverviewAsync_ComposesDomainReports()
    {
        var service = CreateService();
        var result = await service.GetOverviewAsync(null, null);

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(40, result.Data.Business.TotalActiveAccounts);
        Assert.Equal(28, result.Data.Projects.TotalNonTerminal);
        Assert.Equal(10, result.Data.Commercial.QuotationsSentInRange);
        Assert.Equal(9, result.Data.Production.RequestsOpen);
        Assert.Equal(2, result.Data.Delivery.ReadyForDelivery);
        Assert.Equal(120, result.Data.Catalog.ActiveProducts);
        Assert.Equal(15, result.Data.Catalog.ProductsMissing3D);
    }

    [Fact]
    public async Task GetProjectAgingAsync_InvalidBucket_ReturnsBadRequest()
    {
        var service = CreateService();
        var result = await service.GetProjectAgingAsync(new ProjectAgingQueryDto
        {
            ThresholdDays = 7,
            Bucket = "BAD",
            Page = 1,
            PageSize = 20
        });

        Assert.Equal(400, result.Status);
    }

    [Fact]
    public async Task GetCommercialTrendAsync_RequiresDates()
    {
        var service = CreateService();
        var result = await service.GetCommercialTrendAsync(new CommercialTrendQueryDto());
        Assert.Equal(400, result.Status);
        Assert.Equal("From and To dates are required.", result.Message);
    }

    [Fact]
    public async Task GetCommercialTrendAsync_RejectsRangeOver90Days()
    {
        var service = CreateService();
        var result = await service.GetCommercialTrendAsync(new CommercialTrendQueryDto
        {
            From = new DateTime(2026, 1, 1),
            To = new DateTime(2026, 5, 1)
        });

        Assert.Equal(400, result.Status);
        Assert.Equal("Date range must not exceed 90 days.", result.Message);
    }

    [Fact]
    public async Task GetCatalogBestsellersAsync_InvalidMetric_ReturnsBadRequest()
    {
        var service = CreateService();
        var result = await service.GetCatalogBestsellersAsync(new CatalogBestsellersQueryDto
        {
            From = new DateTime(2026, 7, 1),
            To = new DateTime(2026, 7, 31),
            Metric = "units"
        });

        Assert.Equal(400, result.Status);
    }

    [Fact]
    public async Task GetProductionWorkloadAsync_ReturnsPagedItems()
    {
        var service = CreateService();
        var result = await service.GetProductionWorkloadAsync(new ProductionWorkloadQueryDto
        {
            Page = 1,
            PageSize = 20
        });

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data.Items);
        Assert.Equal("AVAILABLE", result.Data.Items[0].CapacityState);
    }

    [Fact]
    public async Task ExportAsync_InvalidDomain_ReturnsBadRequest()
    {
        var service = CreateService();
        var result = await service.ExportAsync(new ReportExportQueryDto { Domain = "finance" });
        Assert.Equal(400, result.Status);
    }

    [Fact]
    public async Task ExportAsync_ReturnsCsvBytes()
    {
        var service = CreateService();
        var result = await service.ExportAsync(new ReportExportQueryDto { Domain = "business", Format = "csv" });

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.StartsWith("report-business-", result.Data.FileName);
        Assert.NotEmpty(result.Data.Content);
    }

    private static AdminReportService CreateService()
    {
        return new AdminReportService(new FakeAdminReportRepository(), new FakeAccountServiceForReports());
    }

    private sealed class FakeAccountServiceForReports : IAccountService
    {
        public Task<ServiceResult<DesignerWorkloadSummaryDto>> GetDesignerWorkloadSummaryAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ServiceResult<DesignerWorkloadSummaryDto>.Success(new DesignerWorkloadSummaryDto
            {
                TotalActiveDesigners = 5,
                AvailableCount = 3,
                FullCount = 1,
                OverCount = 1,
                TotalDesignActiveProjects = 6,
                MaxActiveProjects = 2
            }));
        }

        public Task<ServiceResult<SalesWorkloadSummaryDto>> GetSalesWorkloadSummaryAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ServiceResult<SalesWorkloadSummaryDto>.Success(new SalesWorkloadSummaryDto
            {
                TotalActiveSales = 4,
                AvailableNowCount = 2,
                FullNowCount = 1,
                OverNowCount = 1,
                HighFuturePressureCount = 2,
                TotalSalesActiveProjects = 12,
                UnassignedIntakeCount = 3,
                MaxActiveProjects = 5
            }));
        }

        public Task<ServiceResult<AccountDto>> CreateAsync(CreateAccountRequestDto request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<ServiceResult<AccountDto>> GetByIdAsync(Guid accountId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<ServiceResult<AccountDetailDto>> GetAdminDetailAsync(Guid accountId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<ServiceResult<MyProfileDto>> UpdateMyProfileAsync(Guid currentUserId, UpdateMyProfileRequestDto request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<ServiceResult<PagedResult<AvailableDesignerDto>>> GetAvailableDesignersAsync(AvailableDesignerQueryDto query, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<ServiceResult<PagedResult<AvailableDesignerDto>>> GetDesignerWorkloadAsync(DesignerWorkloadQueryDto query, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<ServiceResult<PagedResult<DesignerAssignedProjectDto>>> GetDesignerAssignedProjectsAsync(Guid designerId, DesignerAssignedProjectQueryDto query, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<ServiceResult<PagedResult<SalesWorkloadItemDto>>> GetSalesWorkloadAsync(SalesWorkloadQueryDto query, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<ServiceResult<PagedResult<SalesAssignedProjectDto>>> GetSalesAssignedProjectsAsync(Guid salesId, SalesAssignedProjectQueryDto query, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<ServiceResult<PagedResult<UnassignedIntakeProjectDto>>> GetUnassignedIntakeProjectsAsync(UnassignedIntakeProjectQueryDto query, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<ServiceResult<PagedResult<AccountDto>>> GetPagedAsync(int page, int pageSize, string? search, string? status, bool includeDeleted, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<ServiceResult<AccountSearchStatsDto>> GetSearchStatsAsync(bool includeDeleted, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<ServiceResult<AccountSuggestResponseDto>> SuggestAsync(string query, int limit, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<ServiceResult<AccountDto>> UpdateAsync(Guid accountId, UpdateAccountRequestDto request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<ServiceResult> DeleteAsync(Guid accountId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class FakeAdminReportRepository : IAdminReportRepository
    {
        public Task<IReadOnlyList<(string Key, long Count)>> CountAccountsByStatusAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<(string, long)>>([("ACTIVE", 40)]);

        public Task<IReadOnlyList<(string Key, long Count, string? Label)>> CountAccountsByRoleAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<(string, long, string?)>>([("SALES", 4, "SALES"), ("DESIGNER", 5, "DESIGNER")]);

        public Task<ProjectReportDto> GetProjectReportAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
            => Task.FromResult(new ProjectReportDto
            {
                TotalNonTerminal = 28,
                CompletedInRange = 3,
                RejectedInRange = 1,
                ByBucket = new ProjectBucketCountsDto { Intake = 5, Commercial = 6, DesignMonitor = 8, Fulfillment = 7, Terminal = 12 }
            });

        public Task<CommercialReportDto> GetCommercialReportAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
            => Task.FromResult(new CommercialReportDto
            {
                Quotations = new CommercialQuotationsDto { SentInRange = 10, AcceptedInRange = 4 },
                Orders = new CommercialOrdersDto { OpenCount = 7, GmvInRange = 150_000_000m, OutstandingAmount = 70_000_000m },
                Payments = new CommercialPaymentsDto { PaidAmountInRange = 80_000_000m }
            });

        public Task<ProductionReportDto> GetProductionReportAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
            => Task.FromResult(new ProductionReportDto
            {
                OpenRequestCount = 9,
                BlockedCount = 2,
                OverdueCount = 1
            });

        public Task<DeliveryReportDto> GetDeliveryReportAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
            => Task.FromResult(new DeliveryReportDto
            {
                Projects = new DeliveryProjectsDto { ReadyForDelivery = 2, Delivering = 1, DeliveredInRange = 3 },
                Schedules = new DeliverySchedulesDto { UpcomingDeliveryOrHandover = 4 }
            });

        public Task<CatalogReportDto> GetCatalogReportAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new CatalogReportDto
            {
                ProductsByStatus = [new ReportFacetCountDto { Key = "ACTIVE", Count = 120 }],
                BusinessTypesByStatus = [new ReportFacetCountDto { Key = "ACTIVE", Count = 12 }],
                ProductsMissingActiveVersion = 8,
                ProductsMissing3D = 15
            });

        public Task<(IReadOnlyList<ProjectAgingItemDto> Items, int Total)> GetProjectAgingAsync(
            int thresholdDays,
            string? bucket,
            string? reason,
            int page,
            int pageSize,
            string sortBy,
            CancellationToken cancellationToken = default)
            => Task.FromResult<(IReadOnlyList<ProjectAgingItemDto>, int)>(([], 0));

        public Task<CommercialTrendDto> GetCommercialTrendAsync(
            DateTime from,
            DateTime to,
            string granularity,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new CommercialTrendDto { Granularity = granularity, From = from, To = to });

        public Task<CatalogBestsellersDto> GetCatalogBestsellersAsync(
            DateTime from,
            DateTime to,
            string metric,
            int limit,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new CatalogBestsellersDto { Metric = metric, From = from, To = to });

        public Task<DeliveryReviewsDto> GetDeliveryReviewsAsync(
            DateTime? from,
            DateTime? to,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new DeliveryReviewsDto { Page = page, PageSize = pageSize });

        public Task<(IReadOnlyList<ProductionWorkloadItemDto> Items, int Total, ProductionWorkloadSummaryDto Summary)> GetProductionWorkloadAsync(
            int page,
            int pageSize,
            int maxActiveRequests,
            string? search,
            string? capacityState,
            string sortBy,
            CancellationToken cancellationToken = default)
        {
            var items = new List<ProductionWorkloadItemDto>
            {
                new()
                {
                    AccountId = Guid.NewGuid(),
                    FullName = "Prod A",
                    Email = "prod@example.com",
                    OpenRequestCount = 2,
                    MaxActiveRequests = maxActiveRequests,
                    AvailableSlot = maxActiveRequests - 2,
                    CapacityState = "AVAILABLE"
                }
            };
            var summary = new ProductionWorkloadSummaryDto
            {
                TotalActiveStaff = 1,
                AvailableCount = 1,
                MaxActiveRequests = maxActiveRequests
            };
            return Task.FromResult<(IReadOnlyList<ProductionWorkloadItemDto>, int, ProductionWorkloadSummaryDto)>((items, 1, summary));
        }
    }
}
