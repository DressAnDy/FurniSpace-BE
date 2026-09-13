#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Accounts;
using FurniSpace.Application.Interfaces.Accounts;
using FurniSpace.Application.Interfaces.Reports;
using FurniSpace.Application.Services.Reports;
using FurniSpace.Infrastructure.Repositories.IRepository;
using FurniSpace.Shared.DTOs.Reports;
using Xunit;

namespace FurniSpace.Application.Tests.Reports;

public sealed class AdminReportServiceCoverageTests
{
    [Fact]
    public async Task DomainReports_InvalidDateRange_ReturnsBadRequest()
    {
        var service = CreateService();
        var from = DateTime.UtcNow.Date.AddDays(1);
        var to = DateTime.UtcNow.Date;

        Assert.Equal(400, (await service.GetProjectsAsync(from, to)).Status);
        Assert.Equal(400, (await service.GetCommercialAsync(from, to)).Status);
        Assert.Equal(400, (await service.GetProductionAsync(from, to)).Status);
        Assert.Equal(400, (await service.GetDeliveryAsync(from, to)).Status);
    }

    [Fact]
    public async Task DomainReports_HappyPaths_Return200()
    {
        var service = CreateService();
        Assert.Equal(200, (await service.GetProjectsAsync(null, null)).Status);
        Assert.Equal(200, (await service.GetCommercialAsync(null, null)).Status);
        Assert.Equal(200, (await service.GetProductionAsync(null, null)).Status);
        Assert.Equal(200, (await service.GetDeliveryAsync(null, null)).Status);
        Assert.Equal(200, (await service.GetCatalogAsync()).Status);
    }

    [Fact]
    public async Task GetBusinessAsync_WhenDesignerSummaryFails_ReturnsBadRequest()
    {
        var service = CreateService(designerFails: true);
        var result = await service.GetBusinessAsync();
        Assert.Equal(400, result.Status);
    }

    [Fact]
    public async Task GetBusinessAsync_WhenSalesSummaryFails_ReturnsBadRequest()
    {
        var service = CreateService(salesFails: true);
        var result = await service.GetBusinessAsync();
        Assert.Equal(400, result.Status);
    }

    [Fact]
    public async Task GetOverviewAsync_WhenBusinessFails_ReturnsBadRequest()
    {
        var service = CreateService(designerFails: true);
        var result = await service.GetOverviewAsync(null, null);
        Assert.Equal(400, result.Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetProjectAgingAsync_InvalidThreshold_ReturnsBadRequest(int days)
    {
        var result = await CreateService().GetProjectAgingAsync(new ProjectAgingQueryDto
        {
            ThresholdDays = days,
            Page = 1,
            PageSize = 20
        });
        Assert.Equal(400, result.Status);
    }

    [Fact]
    public async Task GetProjectAgingAsync_InvalidPageAndPageSize_ReturnsBadRequest()
    {
        var service = CreateService();
        Assert.Equal(400, (await service.GetProjectAgingAsync(new ProjectAgingQueryDto { ThresholdDays = 7, Page = 0, PageSize = 20 })).Status);
        Assert.Equal(400, (await service.GetProjectAgingAsync(new ProjectAgingQueryDto { ThresholdDays = 7, Page = 1, PageSize = 0 })).Status);
        Assert.Equal(400, (await service.GetProjectAgingAsync(new ProjectAgingQueryDto { ThresholdDays = 7, Page = 1, PageSize = 101 })).Status);
    }

    [Fact]
    public async Task GetProjectAgingAsync_InvalidReason_ReturnsBadRequest()
    {
        var result = await CreateService().GetProjectAgingAsync(new ProjectAgingQueryDto
        {
            ThresholdDays = 7,
            Reason = "LATE",
            Page = 1,
            PageSize = 20
        });
        Assert.Equal(400, result.Status);
    }

    [Fact]
    public async Task GetProjectAgingAsync_Success_WithFiltersAndSort()
    {
        var service = CreateService();
        var result = await service.GetProjectAgingAsync(new ProjectAgingQueryDto
        {
            ThresholdDays = 7,
            Bucket = "intake",
            Reason = "stuck",
            Page = 1,
            PageSize = 20,
            SortBy = "SubmittedAtAsc"
        });
        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task GetCommercialTrendAsync_InvalidDateOrderAndGranularity()
    {
        var service = CreateService();
        Assert.Equal(400, (await service.GetCommercialTrendAsync(new CommercialTrendQueryDto
        {
            From = new DateTime(2026, 7, 10),
            To = new DateTime(2026, 7, 1)
        })).Status);

        Assert.Equal(400, (await service.GetCommercialTrendAsync(new CommercialTrendQueryDto
        {
            From = new DateTime(2026, 7, 1),
            To = new DateTime(2026, 7, 10),
            Granularity = "month"
        })).Status);
    }

    [Fact]
    public async Task GetCommercialTrendAsync_Success_DayAndWeek()
    {
        var service = CreateService();
        var day = await service.GetCommercialTrendAsync(new CommercialTrendQueryDto
        {
            From = new DateTime(2026, 7, 1),
            To = new DateTime(2026, 7, 10),
            Granularity = "day"
        });
        Assert.Equal(200, day.Status);

        var week = await service.GetCommercialTrendAsync(new CommercialTrendQueryDto
        {
            From = new DateTime(2026, 7, 1),
            To = new DateTime(2026, 7, 20),
            Granularity = "WEEK"
        });
        Assert.Equal(200, week.Status);
        Assert.Equal("week", week.Data!.Granularity);
    }

    [Fact]
    public async Task GetCatalogBestsellersAsync_ValidationAndSuccess()
    {
        var service = CreateService();
        Assert.Equal(400, (await service.GetCatalogBestsellersAsync(new CatalogBestsellersQueryDto())).Status);
        Assert.Equal(400, (await service.GetCatalogBestsellersAsync(new CatalogBestsellersQueryDto
        {
            From = new DateTime(2026, 7, 10),
            To = new DateTime(2026, 7, 1)
        })).Status);
        Assert.Equal(400, (await service.GetCatalogBestsellersAsync(new CatalogBestsellersQueryDto
        {
            From = new DateTime(2026, 7, 1),
            To = new DateTime(2026, 7, 10),
            Limit = 0
        })).Status);

        var ok = await service.GetCatalogBestsellersAsync(new CatalogBestsellersQueryDto
        {
            From = new DateTime(2026, 7, 1),
            To = new DateTime(2026, 7, 10),
            Metric = "revenue",
            Limit = 10
        });
        Assert.Equal(200, ok.Status);
        Assert.Equal("revenue", ok.Data!.Metric);
    }

    [Fact]
    public async Task GetDeliveryReviewsAsync_ValidationAndSuccess()
    {
        var service = CreateService();
        Assert.Equal(400, (await service.GetDeliveryReviewsAsync(new DeliveryReviewsQueryDto
        {
            From = DateTime.UtcNow.AddDays(1),
            To = DateTime.UtcNow,
            Page = 1,
            PageSize = 20
        })).Status);
        Assert.Equal(400, (await service.GetDeliveryReviewsAsync(new DeliveryReviewsQueryDto { Page = 0 })).Status);
        Assert.Equal(400, (await service.GetDeliveryReviewsAsync(new DeliveryReviewsQueryDto { Page = 1, PageSize = 101 })).Status);

        var ok = await service.GetDeliveryReviewsAsync(new DeliveryReviewsQueryDto { Page = 1, PageSize = 20 });
        Assert.Equal(200, ok.Status);
    }

    [Fact]
    public async Task GetProductionWorkloadAsync_ValidationAndSummary()
    {
        var service = CreateService();
        Assert.Equal(400, (await service.GetProductionWorkloadAsync(new ProductionWorkloadQueryDto { Page = 0 })).Status);
        Assert.Equal(400, (await service.GetProductionWorkloadAsync(new ProductionWorkloadQueryDto { Page = 1, PageSize = 101 })).Status);
        Assert.Equal(400, (await service.GetProductionWorkloadAsync(new ProductionWorkloadQueryDto
        {
            Page = 1,
            PageSize = 20,
            CapacityState = "BUSY"
        })).Status);

        var list = await service.GetProductionWorkloadAsync(new ProductionWorkloadQueryDto
        {
            Page = 1,
            PageSize = 20,
            Search = "Prod",
            CapacityState = "available",
            SortBy = "AvailableSlotDesc"
        });
        Assert.Equal(200, list.Status);

        var summary = await service.GetProductionWorkloadSummaryAsync();
        Assert.Equal(200, summary.Status);
        Assert.Equal(5, summary.Data!.MaxActiveRequests);
    }

    [Theory]
    [InlineData("overview")]
    [InlineData("projects")]
    [InlineData("commercial")]
    [InlineData("production")]
    [InlineData("delivery")]
    [InlineData("catalog")]
    public async Task ExportAsync_AllDomains_Succeed(string domain)
    {
        var result = await CreateService().ExportAsync(new ReportExportQueryDto { Domain = domain });
        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Contains(domain, result.Data.FileName);
    }

    [Fact]
    public async Task ExportAsync_ValidationBranches()
    {
        var service = CreateService();
        Assert.Equal(400, (await service.ExportAsync(new ReportExportQueryDto { Domain = " " })).Status);
        Assert.Equal(400, (await service.ExportAsync(new ReportExportQueryDto { Domain = "business", Format = "xlsx" })).Status);
        Assert.Equal(400, (await service.ExportAsync(new ReportExportQueryDto
        {
            Domain = "business",
            From = DateTime.UtcNow.AddDays(1),
            To = DateTime.UtcNow
        })).Status);
    }

    [Fact]
    public async Task ExportAsync_WhenDomainReportFails_ReturnsBadRequest()
    {
        var service = CreateService(designerFails: true);
        var result = await service.ExportAsync(new ReportExportQueryDto { Domain = "overview" });
        Assert.Equal(400, result.Status);
    }

    private static AdminReportService CreateService(bool designerFails = false, bool salesFails = false)
    {
        return new AdminReportService(
            new FakeAdminReportRepository(),
            new FakeAccountServiceForReports(designerFails, salesFails));
    }

    private sealed class FakeAccountServiceForReports : IAccountService
    {
        private readonly bool _designerFails;
        private readonly bool _salesFails;

        public FakeAccountServiceForReports(bool designerFails, bool salesFails)
        {
            _designerFails = designerFails;
            _salesFails = salesFails;
        }

        public Task<ServiceResult<DesignerWorkloadSummaryDto>> GetDesignerWorkloadSummaryAsync(
            CancellationToken cancellationToken = default)
        {
            if (_designerFails)
            {
                return Task.FromResult(ServiceResult<DesignerWorkloadSummaryDto>.BadRequest("designer fail"));
            }

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
            if (_salesFails)
            {
                return Task.FromResult(ServiceResult<SalesWorkloadSummaryDto>.BadRequest("sales fail"));
            }

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

        public Task<ServiceResult<AccountDto>> CreateAsync(CreateAccountRequestDto request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ServiceResult<AccountDto>> GetByIdAsync(Guid accountId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ServiceResult<AccountDetailDto>> GetAdminDetailAsync(Guid accountId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ServiceResult<MyProfileDto>> UpdateMyProfileAsync(Guid currentUserId, UpdateMyProfileRequestDto request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ServiceResult<PagedResult<AvailableDesignerDto>>> GetAvailableDesignersAsync(AvailableDesignerQueryDto query, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ServiceResult<PagedResult<AvailableDesignerDto>>> GetDesignerWorkloadAsync(DesignerWorkloadQueryDto query, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ServiceResult<PagedResult<DesignerAssignedProjectDto>>> GetDesignerAssignedProjectsAsync(Guid designerId, DesignerAssignedProjectQueryDto query, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ServiceResult<PagedResult<SalesWorkloadItemDto>>> GetSalesWorkloadAsync(SalesWorkloadQueryDto query, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ServiceResult<PagedResult<SalesAssignedProjectDto>>> GetSalesAssignedProjectsAsync(Guid salesId, SalesAssignedProjectQueryDto query, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ServiceResult<PagedResult<UnassignedIntakeProjectDto>>> GetUnassignedIntakeProjectsAsync(UnassignedIntakeProjectQueryDto query, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ServiceResult<PagedResult<AccountDto>>> GetPagedAsync(int page, int pageSize, string? search, string? status, bool includeDeleted, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ServiceResult<AccountSearchStatsDto>> GetSearchStatsAsync(bool includeDeleted, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ServiceResult<AccountSuggestResponseDto>> SuggestAsync(string query, int limit, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ServiceResult<AccountDto>> UpdateAsync(Guid accountId, UpdateAccountRequestDto request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ServiceResult> DeleteAsync(Guid accountId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class FakeAdminReportRepository : IAdminReportRepository
    {
        public Task<IReadOnlyList<(string Key, long Count)>> CountAccountsByStatusAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<(string, long)>>([("ACTIVE", 40)]);

        public Task<IReadOnlyList<(string Key, long Count, string? Label)>> CountAccountsByRoleAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<(string, long, string?)>>([("SALES", 4, "SALES")]);

        public Task<ProjectReportDto> GetProjectReportAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
            => Task.FromResult(new ProjectReportDto { TotalNonTerminal = 1 });

        public Task<CommercialReportDto> GetCommercialReportAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
            => Task.FromResult(new CommercialReportDto());

        public Task<ProductionReportDto> GetProductionReportAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
            => Task.FromResult(new ProductionReportDto());

        public Task<DeliveryReportDto> GetDeliveryReportAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
            => Task.FromResult(new DeliveryReportDto());

        public Task<CatalogReportDto> GetCatalogReportAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new CatalogReportDto
            {
                ProductsByStatus = [new ReportFacetCountDto { Key = "ACTIVE", Count = 1 }],
                BusinessTypesByStatus = [new ReportFacetCountDto { Key = "ACTIVE", Count = 1 }]
            });

        public Task<(IReadOnlyList<ProjectAgingItemDto> Items, int Total)> GetProjectAgingAsync(
            int thresholdDays, string? bucket, string? reason, int page, int pageSize, string sortBy, CancellationToken cancellationToken = default)
            => Task.FromResult<(IReadOnlyList<ProjectAgingItemDto>, int)>(([new ProjectAgingItemDto { ProjectName = "P", Bucket = bucket ?? "INTAKE", Reason = reason ?? "STUCK" }], 1));

        public Task<CommercialTrendDto> GetCommercialTrendAsync(DateTime from, DateTime to, string granularity, CancellationToken cancellationToken = default)
            => Task.FromResult(new CommercialTrendDto { Granularity = granularity, From = from, To = to });

        public Task<CatalogBestsellersDto> GetCatalogBestsellersAsync(DateTime from, DateTime to, string metric, int limit, CancellationToken cancellationToken = default)
            => Task.FromResult(new CatalogBestsellersDto { Metric = metric, From = from, To = to });

        public Task<DeliveryReviewsDto> GetDeliveryReviewsAsync(DateTime? from, DateTime? to, int page, int pageSize, CancellationToken cancellationToken = default)
            => Task.FromResult(new DeliveryReviewsDto { Page = page, PageSize = pageSize });

        public Task<(IReadOnlyList<ProductionWorkloadItemDto> Items, int Total, ProductionWorkloadSummaryDto Summary)> GetProductionWorkloadAsync(
            int page, int pageSize, int maxActiveRequests, string? search, string? capacityState, string sortBy, CancellationToken cancellationToken = default)
        {
            var items = new List<ProductionWorkloadItemDto>
            {
                new()
                {
                    FullName = "Prod A",
                    Email = "prod@example.com",
                    OpenRequestCount = 1,
                    MaxActiveRequests = maxActiveRequests,
                    AvailableSlot = maxActiveRequests - 1,
                    CapacityState = "AVAILABLE"
                }
            };
            return Task.FromResult<(IReadOnlyList<ProductionWorkloadItemDto>, int, ProductionWorkloadSummaryDto)>(
                (items, 1, new ProductionWorkloadSummaryDto { MaxActiveRequests = maxActiveRequests, TotalActiveStaff = 1, AvailableCount = 1 }));
        }
    }
}
