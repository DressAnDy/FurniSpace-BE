#nullable enable

namespace FurniSpace.Shared.DTOs.Reports;

public sealed class ReportFacetCountDto
{
    public string Key { get; set; } = string.Empty;
    public long Count { get; set; }
    public string? Label { get; set; }
}

public sealed class ReportDateRangeQueryDto
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}

public sealed class ProjectBucketCountsDto
{
    public int Intake { get; set; }
    public int Commercial { get; set; }
    public int DesignMonitor { get; set; }
    public int Fulfillment { get; set; }
    public int Terminal { get; set; }
    public int Other { get; set; }
}

public sealed class ProjectAgingCountsDto
{
    public int Over7Days { get; set; }
    public int Over14Days { get; set; }
    public int Over30Days { get; set; }
}

public sealed class BusinessReportDto
{
    public IReadOnlyList<ReportFacetCountDto> AccountsByRole { get; set; } = [];
    public IReadOnlyList<ReportFacetCountDto> AccountsByStatus { get; set; } = [];
    public BusinessDesignerCapacityDto Designer { get; set; } = new();
    public BusinessSalesCapacityDto Sales { get; set; } = new();
}

public sealed class BusinessDesignerCapacityDto
{
    public int TotalActiveDesigners { get; set; }
    public int AvailableCount { get; set; }
    public int FullCount { get; set; }
    public int OverCount { get; set; }
    public int TotalDesignActiveProjects { get; set; }
    public int MaxActiveProjects { get; set; }
}

public sealed class BusinessSalesCapacityDto
{
    public int TotalActiveSales { get; set; }
    public int AvailableNowCount { get; set; }
    public int FullNowCount { get; set; }
    public int OverNowCount { get; set; }
    public int HighFuturePressureCount { get; set; }
    public int TotalSalesActiveProjects { get; set; }
    public int UnassignedIntakeCount { get; set; }
    public int MaxActiveProjects { get; set; }
}

public sealed class ProjectReportDto
{
    public IReadOnlyList<ReportFacetCountDto> ByStatus { get; set; } = [];
    public ProjectBucketCountsDto ByBucket { get; set; } = new();
    public int UnassignedIntakeCount { get; set; }
    public int WaitingForDesignerCount { get; set; }
    public int CompletedInRange { get; set; }
    public int RejectedInRange { get; set; }
    public int TotalNonTerminal { get; set; }
    public ProjectAgingCountsDto Aging { get; set; } = new();
}

public sealed class CommercialReportDto
{
    public CommercialQuotationsDto Quotations { get; set; } = new();
    public CommercialOrdersDto Orders { get; set; } = new();
    public CommercialPaymentsDto Payments { get; set; } = new();
    public CommercialConversionDto Conversion { get; set; } = new();
}

public sealed class CommercialQuotationsDto
{
    public IReadOnlyList<ReportFacetCountDto> ByStatus { get; set; } = [];
    public int SentInRange { get; set; }
    public int AcceptedInRange { get; set; }
    public int RevisionRequestedCount { get; set; }
    public int RevisedCount { get; set; }
}

public sealed class CommercialOrdersDto
{
    public IReadOnlyList<ReportFacetCountDto> ByStatus { get; set; } = [];
    public int OpenCount { get; set; }
    public decimal GmvInRange { get; set; }
    public decimal CollectedTotal { get; set; }
    public decimal OutstandingAmount { get; set; }
    public int CreatedInRange { get; set; }
}

public sealed class CommercialPaymentsDto
{
    public IReadOnlyList<ReportFacetCountDto> ByStatus { get; set; } = [];
    public IReadOnlyList<PaymentTypeAmountDto> ByType { get; set; } = [];
    public decimal PaidAmountInRange { get; set; }
    public int ExpiredCount { get; set; }
    public int CancelledCount { get; set; }
}

public sealed class PaymentTypeAmountDto
{
    public string Type { get; set; } = string.Empty;
    public long Count { get; set; }
    public decimal Amount { get; set; }
}

public sealed class CommercialConversionDto
{
    public int ProjectsInCommercialBucket { get; set; }
    public int OrdersCreatedInRange { get; set; }
    public int DepositsPaidInRange { get; set; }
}

public sealed class ProductionReportDto
{
    public IReadOnlyList<ReportFacetCountDto> RequestsByStatus { get; set; } = [];
    public IReadOnlyList<ReportFacetCountDto> ItemsByStatus { get; set; } = [];
    public int OpenRequestCount { get; set; }
    public int BlockedCount { get; set; }
    public int PendingReviewCount { get; set; }
    public int UnassignedCount { get; set; }
    public int OverdueCount { get; set; }
    public int CreatedInRange { get; set; }
    public int CompletedInRange { get; set; }
    public IReadOnlyList<ProductionAssigneeLoadDto> TopAssignees { get; set; } = [];
}

public sealed class ProductionAssigneeLoadDto
{
    public Guid AccountId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public int OpenCount { get; set; }
    public int OverdueCount { get; set; }
}

public sealed class DeliveryReportDto
{
    public DeliveryProjectsDto Projects { get; set; } = new();
    public DeliveryOrdersDto Orders { get; set; } = new();
    public DeliveryOrderItemsDto OrderItems { get; set; } = new();
    public DeliverySchedulesDto Schedules { get; set; } = new();
}

public sealed class DeliveryProjectsDto
{
    public int ReadyForDelivery { get; set; }
    public int Delivering { get; set; }
    public int DeliveredInRange { get; set; }
}

public sealed class DeliveryOrdersDto
{
    public IReadOnlyList<ReportFacetCountDto> DeliveryRelatedByStatus { get; set; } = [];
    public int CustomerConfirmedInRange { get; set; }
}

public sealed class DeliveryOrderItemsDto
{
    public int PartialDeliveryCount { get; set; }
}

public sealed class DeliverySchedulesDto
{
    public int UpcomingDeliveryOrHandover { get; set; }
    public int OverdueDeliveryOrHandover { get; set; }
}

public sealed class CatalogReportDto
{
    public IReadOnlyList<ReportFacetCountDto> ProductsByStatus { get; set; } = [];
    public IReadOnlyList<ReportFacetCountDto> CategoriesByStatus { get; set; } = [];
    public IReadOnlyList<ReportFacetCountDto> BusinessTypesByStatus { get; set; } = [];
    public IReadOnlyList<ReportFacetCountDto> VersionsByStatus { get; set; } = [];
    public int ProductsMissingActiveVersion { get; set; }
    public int ProductsMissing3D { get; set; }
    public IReadOnlyList<NamedCountDto> ProductsByCategory { get; set; } = [];
    public IReadOnlyList<NamedCountDto> ProductsByBusinessType { get; set; } = [];
}

public sealed class NamedCountDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public long Count { get; set; }
}

public sealed class ReportOverviewDto
{
    public ReportOverviewBusinessDto Business { get; set; } = new();
    public ReportOverviewProjectsDto Projects { get; set; } = new();
    public ReportOverviewCommercialDto Commercial { get; set; } = new();
    public ReportOverviewProductionDto Production { get; set; } = new();
    public ReportOverviewDeliveryDto Delivery { get; set; } = new();
    public ReportOverviewCatalogDto Catalog { get; set; } = new();
}

public sealed class ReportOverviewBusinessDto
{
    public int TotalActiveAccounts { get; set; }
    public int DesignerAvailableCount { get; set; }
    public int DesignerFullCount { get; set; }
    public int DesignerOverCount { get; set; }
    public int SalesAvailableNowCount { get; set; }
    public int SalesFullNowCount { get; set; }
    public int SalesOverNowCount { get; set; }
    public int SalesHighFuturePressureCount { get; set; }
    public int UnassignedIntakeCount { get; set; }
}

public sealed class ReportOverviewProjectsDto
{
    public int TotalNonTerminal { get; set; }
    public ProjectBucketCountsDto ByBucket { get; set; } = new();
    public int CompletedInRange { get; set; }
    public int RejectedInRange { get; set; }
}

public sealed class ReportOverviewCommercialDto
{
    public int QuotationsSentInRange { get; set; }
    public int QuotationsAcceptedInRange { get; set; }
    public int OrdersOpen { get; set; }
    public decimal GmvInRange { get; set; }
    public decimal CollectedInRange { get; set; }
    public decimal OutstandingAmount { get; set; }
}

public sealed class ReportOverviewProductionDto
{
    public int RequestsOpen { get; set; }
    public int BlockedCount { get; set; }
    public int OverdueCount { get; set; }
}

public sealed class ReportOverviewDeliveryDto
{
    public int ReadyForDelivery { get; set; }
    public int Delivering { get; set; }
    public int DeliveredInRange { get; set; }
    public int UpcomingSchedules { get; set; }
}

public sealed class ReportOverviewCatalogDto
{
    public int ActiveProducts { get; set; }
    public int ProductsMissingActiveVersion { get; set; }
    public int ProductsMissing3D { get; set; }
    public int ActiveBusinessTypes { get; set; }
}

public sealed class ProjectAgingQueryDto
{
    public int ThresholdDays { get; set; } = 7;
    public string? Bucket { get; set; }
    public string? Reason { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SortBy { get; set; }
}

public sealed class ProjectAgingItemDto
{
    public Guid ProjectId { get; set; }
    public string? ProjectCode { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string? Status { get; set; }
    public string Bucket { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime? SubmittedAt { get; set; }
    public int AgeDays { get; set; }
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public Guid? AssignedSalesId { get; set; }
    public string? SalesName { get; set; }
    public Guid? AssignedDesignerId { get; set; }
    public string? DesignerName { get; set; }
}

public sealed class CommercialTrendQueryDto
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public string? Granularity { get; set; }
}

public sealed class CommercialTrendDto
{
    public string Granularity { get; set; } = "day";
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public IReadOnlyList<CommercialTrendPointDto> Points { get; set; } = [];
    public CommercialTrendTotalsDto Totals { get; set; } = new();
}

public sealed class CommercialTrendPointDto
{
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public int QuotationsSent { get; set; }
    public int QuotationsAccepted { get; set; }
    public int OrdersCreated { get; set; }
    public decimal Gmv { get; set; }
    public decimal Collected { get; set; }
}

public sealed class CommercialTrendTotalsDto
{
    public int QuotationsSent { get; set; }
    public int QuotationsAccepted { get; set; }
    public int OrdersCreated { get; set; }
    public decimal Gmv { get; set; }
    public decimal Collected { get; set; }
}

public sealed class CatalogBestsellersQueryDto
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public string? Metric { get; set; }
    public int Limit { get; set; } = 20;
}

public sealed class CatalogBestsellersDto
{
    public string Metric { get; set; } = "quantity";
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public IReadOnlyList<CatalogBestsellerItemDto> Items { get; set; } = [];
}

public sealed class CatalogBestsellerItemDto
{
    public Guid? ProductId { get; set; }
    public string? ProductName { get; set; }
    public Guid? ProductVersionId { get; set; }
    public string? Sku { get; set; }
    public int QuantitySold { get; set; }
    public decimal Revenue { get; set; }
}

public sealed class DeliveryReviewsQueryDto
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed class DeliveryReviewsDto
{
    public DeliveryReviewsSummaryDto Summary { get; set; } = new();
    public IReadOnlyList<DeliveryReviewItemDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
    public bool HasPreviousPage { get; set; }
    public bool HasNextPage { get; set; }
}

public sealed class DeliveryReviewsSummaryDto
{
    public int ReviewCount { get; set; }
    public double? AverageOverallRating { get; set; }
    public double? AverageDeliveryRating { get; set; }
}

public sealed class DeliveryReviewItemDto
{
    public Guid ProjectId { get; set; }
    public string? ProjectCode { get; set; }
    public int? OverallRating { get; set; }
    public int? DeliveryRating { get; set; }
    public string? Comment { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public sealed class ProductionWorkloadItemDto
{
    public Guid AccountId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int OpenRequestCount { get; set; }
    public int BlockedCount { get; set; }
    public int OverdueCount { get; set; }
    public int MaxActiveRequests { get; set; }
    public int AvailableSlot { get; set; }
    public string CapacityState { get; set; } = string.Empty;
}

public sealed class ProductionWorkloadSummaryDto
{
    public int TotalActiveStaff { get; set; }
    public int AvailableCount { get; set; }
    public int FullCount { get; set; }
    public int OverCount { get; set; }
    public int TotalOpenRequests { get; set; }
    public int BlockedCount { get; set; }
    public int OverdueCount { get; set; }
    public int MaxActiveRequests { get; set; }
}

public sealed class ProductionWorkloadQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public string? CapacityState { get; set; }
    public string? SortBy { get; set; }
}

public sealed class ReportExportQueryDto
{
    public string Domain { get; set; } = string.Empty;
    public string? Format { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}

public sealed class ReportExportFileDto
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "text/csv; charset=utf-8";
    public byte[] Content { get; set; } = [];
}
