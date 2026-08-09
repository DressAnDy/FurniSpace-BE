namespace FurniSpace.Application.Constants.Reports;

internal static class AdminReportServiceConstants
{
    internal const int MaxActiveProductionRequests = 5;
    internal const int MaxTrendRangeDays = 90;

    internal const string PageMustBeGreaterThanZero = "Page must be greater than zero.";
    internal const string PageSizeMustBeBetween1And100 = "Page size must be between 1 and 100.";
    internal const string FromMustBeLessOrEqualTo = "From date must be less than or equal to To date.";
    internal const string FromAndToRequired = "From and To dates are required.";
    internal const string DateRangeMustNotExceed90Days = "Date range must not exceed 90 days.";
    internal const string ThresholdDaysMustBeGreaterThanZero = "Threshold days must be greater than zero.";
    internal const string BucketInvalid = "Bucket must be INTAKE, COMMERCIAL, DESIGN_MONITOR, or FULFILLMENT.";
    internal const string ReasonInvalid = "Reason must be UNASSIGNED_INTAKE, WAITING_DESIGNER, or STUCK.";
    internal const string GranularityInvalid = "Granularity must be day or week.";
    internal const string DomainRequired = "Domain is required.";
    internal const string DomainInvalid = "Domain must be overview, business, projects, commercial, production, delivery, or catalog.";
    internal const string FormatInvalid = "Format must be csv.";
    internal const string MetricInvalid = "Metric must be quantity or revenue.";
    internal const string LimitMustBeBetween1And50 = "Limit must be between 1 and 50.";
    internal const string CapacityStateInvalid = "Capacity state must be AVAILABLE, FULL, or OVER.";

    internal const string OverviewRetrieved = "Report overview retrieved successfully.";
    internal const string BusinessRetrieved = "Business report retrieved successfully.";
    internal const string ProjectsRetrieved = "Project report retrieved successfully.";
    internal const string CommercialRetrieved = "Commercial report retrieved successfully.";
    internal const string ProductionRetrieved = "Production report retrieved successfully.";
    internal const string DeliveryRetrieved = "Delivery report retrieved successfully.";
    internal const string CatalogRetrieved = "Catalog report retrieved successfully.";
    internal const string AgingRetrieved = "Project aging report retrieved successfully.";
    internal const string TrendRetrieved = "Commercial trend retrieved successfully.";
    internal const string BestsellersRetrieved = "Catalog bestsellers retrieved successfully.";
    internal const string ReviewsRetrieved = "Delivery reviews retrieved successfully.";
    internal const string ProductionWorkloadRetrieved = "Production workload retrieved successfully.";
    internal const string ProductionWorkloadSummaryRetrieved = "Production workload summary retrieved successfully.";
    internal const string ExportRetrieved = "Report export generated successfully.";
}
