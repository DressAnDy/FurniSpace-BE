using System.Globalization;
using System.Text;
using System.Text.Json;
using FurniSpace.Application.Common;
using FurniSpace.Application.Interfaces.Accounts;
using FurniSpace.Application.Interfaces.Reports;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Repositories.IRepository;
using FurniSpace.Shared.DTOs.Reports;
using static FurniSpace.Application.Constants.Reports.AdminReportServiceConstants;

namespace FurniSpace.Application.Services.Reports;

public sealed class AdminReportService : IAdminReportService
{
    private static readonly HashSet<string> AllowedAgingBuckets = new(StringComparer.OrdinalIgnoreCase)
    {
        "INTAKE",
        "COMMERCIAL",
        "DESIGN_MONITOR",
        "FULFILLMENT"
    };

    private static readonly HashSet<string> AllowedAgingReasons = new(StringComparer.OrdinalIgnoreCase)
    {
        "UNASSIGNED_INTAKE",
        "WAITING_DESIGNER",
        "STUCK"
    };

    private static readonly HashSet<string> AllowedExportDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "overview",
        "business",
        "projects",
        "commercial",
        "production",
        "delivery",
        "catalog"
    };

    private static readonly HashSet<string> AllowedCapacityStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "AVAILABLE",
        "FULL",
        "OVER"
    };

    private readonly IAdminReportRepository _reports;
    private readonly IAccountService _accounts;

    public AdminReportService(IAdminReportRepository reports, IAccountService accounts)
    {
        _reports = reports;
        _accounts = accounts;
    }

    public async Task<ServiceResult<ReportOverviewDto>> GetOverviewAsync(
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default)
    {
        var dateError = ValidateOptionalDateRange(from, to);
        if (dateError is not null)
        {
            return ServiceResult<ReportOverviewDto>.BadRequest(dateError);
        }

        var business = await GetBusinessAsync(cancellationToken);
        if (business.Status != 200 || business.Data is null)
        {
            return ServiceResult<ReportOverviewDto>.BadRequest(business.Message ?? "Failed to load business report.");
        }

        var projects = await _reports.GetProjectReportAsync(from, to, cancellationToken);
        var commercial = await _reports.GetCommercialReportAsync(from, to, cancellationToken);
        var production = await _reports.GetProductionReportAsync(from, to, cancellationToken);
        var delivery = await _reports.GetDeliveryReportAsync(from, to, cancellationToken);
        var catalog = await _reports.GetCatalogReportAsync(cancellationToken);

        var totalActiveAccounts = (int)business.Data.AccountsByStatus
            .Where(item => string.Equals(item.Key, "ACTIVE", StringComparison.OrdinalIgnoreCase))
            .Sum(item => item.Count);

        var activeProducts = (int)catalog.ProductsByStatus
            .Where(item => string.Equals(item.Key, ProductStatus.ACTIVE.ToString(), StringComparison.OrdinalIgnoreCase))
            .Sum(item => item.Count);

        var activeBusinessTypes = (int)catalog.BusinessTypesByStatus
            .Where(item => string.Equals(item.Key, "ACTIVE", StringComparison.OrdinalIgnoreCase))
            .Sum(item => item.Count);

        var overview = new ReportOverviewDto
        {
            Business = new ReportOverviewBusinessDto
            {
                TotalActiveAccounts = totalActiveAccounts,
                DesignerAvailableCount = business.Data.Designer.AvailableCount,
                DesignerFullCount = business.Data.Designer.FullCount,
                DesignerOverCount = business.Data.Designer.OverCount,
                SalesAvailableNowCount = business.Data.Sales.AvailableNowCount,
                SalesFullNowCount = business.Data.Sales.FullNowCount,
                SalesOverNowCount = business.Data.Sales.OverNowCount,
                SalesHighFuturePressureCount = business.Data.Sales.HighFuturePressureCount,
                UnassignedIntakeCount = business.Data.Sales.UnassignedIntakeCount
            },
            Projects = new ReportOverviewProjectsDto
            {
                TotalNonTerminal = projects.TotalNonTerminal,
                ByBucket = projects.ByBucket,
                CompletedInRange = projects.CompletedInRange,
                RejectedInRange = projects.RejectedInRange
            },
            Commercial = new ReportOverviewCommercialDto
            {
                QuotationsSentInRange = commercial.Quotations.SentInRange,
                QuotationsAcceptedInRange = commercial.Quotations.AcceptedInRange,
                OrdersOpen = commercial.Orders.OpenCount,
                GmvInRange = commercial.Orders.GmvInRange,
                CollectedInRange = commercial.Payments.PaidAmountInRange,
                OutstandingAmount = commercial.Orders.OutstandingAmount
            },
            Production = new ReportOverviewProductionDto
            {
                RequestsOpen = production.OpenRequestCount,
                BlockedCount = production.BlockedCount,
                OverdueCount = production.OverdueCount
            },
            Delivery = new ReportOverviewDeliveryDto
            {
                ReadyForDelivery = delivery.Projects.ReadyForDelivery,
                Delivering = delivery.Projects.Delivering,
                DeliveredInRange = delivery.Projects.DeliveredInRange,
                UpcomingSchedules = delivery.Schedules.UpcomingDeliveryOrHandover
            },
            Catalog = new ReportOverviewCatalogDto
            {
                ActiveProducts = activeProducts,
                ProductsMissingActiveVersion = catalog.ProductsMissingActiveVersion,
                ProductsMissing3D = catalog.ProductsMissing3D,
                ActiveBusinessTypes = activeBusinessTypes
            }
        };

        return ServiceResult<ReportOverviewDto>.Success(overview, OverviewRetrieved);
    }

    public async Task<ServiceResult<BusinessReportDto>> GetBusinessAsync(CancellationToken cancellationToken = default)
    {
        var designerResult = await _accounts.GetDesignerWorkloadSummaryAsync(cancellationToken);
        var salesResult = await _accounts.GetSalesWorkloadSummaryAsync(cancellationToken);
        if (designerResult.Status != 200 || designerResult.Data is null)
        {
            return ServiceResult<BusinessReportDto>.BadRequest(
                designerResult.Message ?? "Failed to load designer workload summary.");
        }

        if (salesResult.Status != 200 || salesResult.Data is null)
        {
            return ServiceResult<BusinessReportDto>.BadRequest(
                salesResult.Message ?? "Failed to load sales workload summary.");
        }

        var byStatus = await _reports.CountAccountsByStatusAsync(cancellationToken);
        var byRole = await _reports.CountAccountsByRoleAsync(cancellationToken);

        var dto = new BusinessReportDto
        {
            AccountsByRole = byRole
                .Select(row => new ReportFacetCountDto { Key = row.Key, Count = row.Count, Label = row.Label })
                .ToList(),
            AccountsByStatus = byStatus
                .Select(row => new ReportFacetCountDto { Key = row.Key, Count = row.Count })
                .ToList(),
            Designer = new BusinessDesignerCapacityDto
            {
                TotalActiveDesigners = designerResult.Data.TotalActiveDesigners,
                AvailableCount = designerResult.Data.AvailableCount,
                FullCount = designerResult.Data.FullCount,
                OverCount = designerResult.Data.OverCount,
                TotalDesignActiveProjects = designerResult.Data.TotalDesignActiveProjects,
                MaxActiveProjects = designerResult.Data.MaxActiveProjects
            },
            Sales = new BusinessSalesCapacityDto
            {
                TotalActiveSales = salesResult.Data.TotalActiveSales,
                AvailableNowCount = salesResult.Data.AvailableNowCount,
                FullNowCount = salesResult.Data.FullNowCount,
                OverNowCount = salesResult.Data.OverNowCount,
                HighFuturePressureCount = salesResult.Data.HighFuturePressureCount,
                TotalSalesActiveProjects = salesResult.Data.TotalSalesActiveProjects,
                UnassignedIntakeCount = salesResult.Data.UnassignedIntakeCount,
                MaxActiveProjects = salesResult.Data.MaxActiveProjects
            }
        };

        return ServiceResult<BusinessReportDto>.Success(dto, BusinessRetrieved);
    }

    public async Task<ServiceResult<ProjectReportDto>> GetProjectsAsync(
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default)
    {
        var dateError = ValidateOptionalDateRange(from, to);
        if (dateError is not null)
        {
            return ServiceResult<ProjectReportDto>.BadRequest(dateError);
        }

        var report = await _reports.GetProjectReportAsync(from, to, cancellationToken);
        return ServiceResult<ProjectReportDto>.Success(report, ProjectsRetrieved);
    }

    public async Task<ServiceResult<CommercialReportDto>> GetCommercialAsync(
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default)
    {
        var dateError = ValidateOptionalDateRange(from, to);
        if (dateError is not null)
        {
            return ServiceResult<CommercialReportDto>.BadRequest(dateError);
        }

        var report = await _reports.GetCommercialReportAsync(from, to, cancellationToken);
        return ServiceResult<CommercialReportDto>.Success(report, CommercialRetrieved);
    }

    public async Task<ServiceResult<ProductionReportDto>> GetProductionAsync(
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default)
    {
        var dateError = ValidateOptionalDateRange(from, to);
        if (dateError is not null)
        {
            return ServiceResult<ProductionReportDto>.BadRequest(dateError);
        }

        var report = await _reports.GetProductionReportAsync(from, to, cancellationToken);
        return ServiceResult<ProductionReportDto>.Success(report, ProductionRetrieved);
    }

    public async Task<ServiceResult<DeliveryReportDto>> GetDeliveryAsync(
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default)
    {
        var dateError = ValidateOptionalDateRange(from, to);
        if (dateError is not null)
        {
            return ServiceResult<DeliveryReportDto>.BadRequest(dateError);
        }

        var report = await _reports.GetDeliveryReportAsync(from, to, cancellationToken);
        return ServiceResult<DeliveryReportDto>.Success(report, DeliveryRetrieved);
    }

    public async Task<ServiceResult<CatalogReportDto>> GetCatalogAsync(CancellationToken cancellationToken = default)
    {
        var report = await _reports.GetCatalogReportAsync(cancellationToken);
        return ServiceResult<CatalogReportDto>.Success(report, CatalogRetrieved);
    }

    public async Task<ServiceResult<PagedResult<ProjectAgingItemDto>>> GetProjectAgingAsync(
        ProjectAgingQueryDto query,
        CancellationToken cancellationToken = default)
    {
        if (query.ThresholdDays < 1)
        {
            return ServiceResult<PagedResult<ProjectAgingItemDto>>.BadRequest(ThresholdDaysMustBeGreaterThanZero);
        }

        if (query.Page < 1)
        {
            return ServiceResult<PagedResult<ProjectAgingItemDto>>.BadRequest(PageMustBeGreaterThanZero);
        }

        if (query.PageSize is < 1 or > 100)
        {
            return ServiceResult<PagedResult<ProjectAgingItemDto>>.BadRequest(PageSizeMustBeBetween1And100);
        }

        if (!string.IsNullOrWhiteSpace(query.Bucket) && !AllowedAgingBuckets.Contains(query.Bucket.Trim()))
        {
            return ServiceResult<PagedResult<ProjectAgingItemDto>>.BadRequest(BucketInvalid);
        }

        if (!string.IsNullOrWhiteSpace(query.Reason) && !AllowedAgingReasons.Contains(query.Reason.Trim()))
        {
            return ServiceResult<PagedResult<ProjectAgingItemDto>>.BadRequest(ReasonInvalid);
        }

        var sortBy = string.Equals(query.SortBy, "SubmittedAtAsc", StringComparison.OrdinalIgnoreCase)
            ? "SubmittedAtAsc"
            : "AgeDaysDesc";

        var (items, total) = await _reports.GetProjectAgingAsync(
            query.ThresholdDays,
            NormalizeOptional(query.Bucket)?.ToUpperInvariant(),
            NormalizeOptional(query.Reason)?.ToUpperInvariant(),
            query.Page,
            query.PageSize,
            sortBy,
            cancellationToken);

        var page = PagedResult<ProjectAgingItemDto>.Create(items, query.Page, query.PageSize, total);
        return ServiceResult<PagedResult<ProjectAgingItemDto>>.Success(page, AgingRetrieved);
    }

    public async Task<ServiceResult<CommercialTrendDto>> GetCommercialTrendAsync(
        CommercialTrendQueryDto query,
        CancellationToken cancellationToken = default)
    {
        if (query.From == default || query.To == default)
        {
            return ServiceResult<CommercialTrendDto>.BadRequest(FromAndToRequired);
        }

        if (query.From > query.To)
        {
            return ServiceResult<CommercialTrendDto>.BadRequest(FromMustBeLessOrEqualTo);
        }

        if ((query.To.Date - query.From.Date).TotalDays > MaxTrendRangeDays)
        {
            return ServiceResult<CommercialTrendDto>.BadRequest(DateRangeMustNotExceed90Days);
        }

        var granularity = NormalizeOptional(query.Granularity) ?? "day";
        if (!string.Equals(granularity, "day", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(granularity, "week", StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<CommercialTrendDto>.BadRequest(GranularityInvalid);
        }

        var trend = await _reports.GetCommercialTrendAsync(
            query.From,
            query.To,
            granularity.ToLowerInvariant(),
            cancellationToken);

        return ServiceResult<CommercialTrendDto>.Success(trend, TrendRetrieved);
    }

    public async Task<ServiceResult<CatalogBestsellersDto>> GetCatalogBestsellersAsync(
        CatalogBestsellersQueryDto query,
        CancellationToken cancellationToken = default)
    {
        if (query.From == default || query.To == default)
        {
            return ServiceResult<CatalogBestsellersDto>.BadRequest(FromAndToRequired);
        }

        if (query.From > query.To)
        {
            return ServiceResult<CatalogBestsellersDto>.BadRequest(FromMustBeLessOrEqualTo);
        }

        if (query.Limit is < 1 or > 50)
        {
            return ServiceResult<CatalogBestsellersDto>.BadRequest(LimitMustBeBetween1And50);
        }

        var metric = NormalizeOptional(query.Metric) ?? "quantity";
        if (!string.Equals(metric, "quantity", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(metric, "revenue", StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<CatalogBestsellersDto>.BadRequest(MetricInvalid);
        }

        var report = await _reports.GetCatalogBestsellersAsync(
            query.From,
            query.To,
            metric.ToLowerInvariant(),
            query.Limit,
            cancellationToken);

        return ServiceResult<CatalogBestsellersDto>.Success(report, BestsellersRetrieved);
    }

    public async Task<ServiceResult<DeliveryReviewsDto>> GetDeliveryReviewsAsync(
        DeliveryReviewsQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var dateError = ValidateOptionalDateRange(query.From, query.To);
        if (dateError is not null)
        {
            return ServiceResult<DeliveryReviewsDto>.BadRequest(dateError);
        }

        if (query.Page < 1)
        {
            return ServiceResult<DeliveryReviewsDto>.BadRequest(PageMustBeGreaterThanZero);
        }

        if (query.PageSize is < 1 or > 100)
        {
            return ServiceResult<DeliveryReviewsDto>.BadRequest(PageSizeMustBeBetween1And100);
        }

        var report = await _reports.GetDeliveryReviewsAsync(
            query.From,
            query.To,
            query.Page,
            query.PageSize,
            cancellationToken);

        return ServiceResult<DeliveryReviewsDto>.Success(report, ReviewsRetrieved);
    }

    public async Task<ServiceResult<PagedResult<ProductionWorkloadItemDto>>> GetProductionWorkloadAsync(
        ProductionWorkloadQueryDto query,
        CancellationToken cancellationToken = default)
    {
        if (query.Page < 1)
        {
            return ServiceResult<PagedResult<ProductionWorkloadItemDto>>.BadRequest(PageMustBeGreaterThanZero);
        }

        if (query.PageSize is < 1 or > 100)
        {
            return ServiceResult<PagedResult<ProductionWorkloadItemDto>>.BadRequest(PageSizeMustBeBetween1And100);
        }

        if (!string.IsNullOrWhiteSpace(query.CapacityState) &&
            !AllowedCapacityStates.Contains(query.CapacityState.Trim()))
        {
            return ServiceResult<PagedResult<ProductionWorkloadItemDto>>.BadRequest(CapacityStateInvalid);
        }

        var sortBy = string.Equals(query.SortBy, "AvailableSlotDesc", StringComparison.OrdinalIgnoreCase)
            ? "AvailableSlotDesc"
            : "OpenRequestCountDesc";

        var (items, total, _) = await _reports.GetProductionWorkloadAsync(
            query.Page,
            query.PageSize,
            MaxActiveProductionRequests,
            NormalizeOptional(query.Search),
            NormalizeOptional(query.CapacityState)?.ToUpperInvariant(),
            sortBy,
            cancellationToken);

        var page = PagedResult<ProductionWorkloadItemDto>.Create(items, query.Page, query.PageSize, total);
        return ServiceResult<PagedResult<ProductionWorkloadItemDto>>.Success(page, ProductionWorkloadRetrieved);
    }

    public async Task<ServiceResult<ProductionWorkloadSummaryDto>> GetProductionWorkloadSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        var (_, _, summary) = await _reports.GetProductionWorkloadAsync(
            page: 1,
            pageSize: 1,
            maxActiveRequests: MaxActiveProductionRequests,
            search: null,
            capacityState: null,
            sortBy: "OpenRequestCountDesc",
            cancellationToken);

        return ServiceResult<ProductionWorkloadSummaryDto>.Success(summary, ProductionWorkloadSummaryRetrieved);
    }

    public async Task<ServiceResult<ReportExportFileDto>> ExportAsync(
        ReportExportQueryDto query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query.Domain))
        {
            return ServiceResult<ReportExportFileDto>.BadRequest(DomainRequired);
        }

        var domain = query.Domain.Trim().ToLowerInvariant();
        if (!AllowedExportDomains.Contains(domain))
        {
            return ServiceResult<ReportExportFileDto>.BadRequest(DomainInvalid);
        }

        var format = NormalizeOptional(query.Format) ?? "csv";
        if (!string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<ReportExportFileDto>.BadRequest(FormatInvalid);
        }

        var dateError = ValidateOptionalDateRange(query.From, query.To);
        if (dateError is not null)
        {
            return ServiceResult<ReportExportFileDto>.BadRequest(dateError);
        }

        object? payload = domain switch
        {
            "overview" => (await GetOverviewAsync(query.From, query.To, cancellationToken)).Data,
            "business" => (await GetBusinessAsync(cancellationToken)).Data,
            "projects" => (await GetProjectsAsync(query.From, query.To, cancellationToken)).Data,
            "commercial" => (await GetCommercialAsync(query.From, query.To, cancellationToken)).Data,
            "production" => (await GetProductionAsync(query.From, query.To, cancellationToken)).Data,
            "delivery" => (await GetDeliveryAsync(query.From, query.To, cancellationToken)).Data,
            "catalog" => (await GetCatalogAsync(cancellationToken)).Data,
            _ => null
        };

        if (payload is null)
        {
            return ServiceResult<ReportExportFileDto>.BadRequest("Failed to build export payload.");
        }

        var csv = BuildJsonFlattenCsv(domain, payload);
        var file = new ReportExportFileDto
        {
            FileName = $"report-{domain}-{DateTime.UtcNow:yyyyMMdd}.csv",
            ContentType = "text/csv; charset=utf-8",
            Content = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray()
        };

        return ServiceResult<ReportExportFileDto>.Success(file, ExportRetrieved);
    }

    private static string? ValidateOptionalDateRange(DateTime? from, DateTime? to)
    {
        if (from.HasValue && to.HasValue && from.Value > to.Value)
        {
            return FromMustBeLessOrEqualTo;
        }

        return null;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string BuildJsonFlattenCsv(string domain, object payload)
    {
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        var builder = new StringBuilder();
        builder.AppendLine("domain,exportedAtUtc,payloadJson");
        builder.Append(EscapeCsv(domain));
        builder.Append(',');
        builder.Append(EscapeCsv(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)));
        builder.Append(',');
        builder.AppendLine(EscapeCsv(json));
        return builder.ToString();
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
