#nullable enable

using System;
using FurniSpace.Shared.DTOs.Reports;
using Xunit;

namespace FurniSpace.Application.Tests.Reports;

/// <summary>
/// Touches Shared report DTO property surfaces for Sonar coverage on auto-properties.
/// </summary>
public sealed class AdminReportDtoCoverageTests
{
    [Fact]
    public void ReportDtos_ExposeAssignedProperties()
    {
        var facet = new ReportFacetCountDto { Key = "ACTIVE", Count = 3, Label = "Active" };
        Assert.Equal("ACTIVE", facet.Key);
        Assert.Equal(3, facet.Count);
        Assert.Equal("Active", facet.Label);

        var range = new ReportDateRangeQueryDto { From = DateTime.UtcNow.AddDays(-1), To = DateTime.UtcNow };
        Assert.NotNull(range.From);
        Assert.NotNull(range.To);

        var buckets = new ProjectBucketCountsDto
        {
            Intake = 1, Commercial = 2, DesignMonitor = 3, Fulfillment = 4, Terminal = 5, Other = 6
        };
        Assert.Equal(21, buckets.Intake + buckets.Commercial + buckets.DesignMonitor + buckets.Fulfillment + buckets.Terminal + buckets.Other);

        var agingCounts = new ProjectAgingCountsDto { Over7Days = 1, Over14Days = 2, Over30Days = 3 };
        Assert.Equal(3, agingCounts.Over30Days);

        var designer = new BusinessDesignerCapacityDto
        {
            TotalActiveDesigners = 1, AvailableCount = 1, FullCount = 0, OverCount = 0,
            TotalDesignActiveProjects = 1, MaxActiveProjects = 2
        };
        var sales = new BusinessSalesCapacityDto
        {
            TotalActiveSales = 1, AvailableNowCount = 1, FullNowCount = 0, OverNowCount = 0,
            HighFuturePressureCount = 1, TotalSalesActiveProjects = 2, UnassignedIntakeCount = 1, MaxActiveProjects = 5
        };
        var business = new BusinessReportDto
        {
            AccountsByRole = [facet],
            AccountsByStatus = [facet],
            Designer = designer,
            Sales = sales
        };
        Assert.Equal(1, business.Designer.TotalActiveDesigners);
        Assert.Equal(5, business.Sales.MaxActiveProjects);

        var project = new ProjectReportDto
        {
            ByStatus = [facet],
            ByBucket = buckets,
            UnassignedIntakeCount = 1,
            WaitingForDesignerCount = 2,
            CompletedInRange = 3,
            RejectedInRange = 4,
            TotalNonTerminal = 5,
            Aging = agingCounts
        };
        Assert.Equal(5, project.TotalNonTerminal);

        var commercial = new CommercialReportDto
        {
            Quotations = new CommercialQuotationsDto
            {
                ByStatus = [facet], SentInRange = 1, AcceptedInRange = 2, RevisionRequestedCount = 3, RevisedCount = 4
            },
            Orders = new CommercialOrdersDto
            {
                ByStatus = [facet], OpenCount = 1, GmvInRange = 2m, CollectedTotal = 3m, OutstandingAmount = 4m, CreatedInRange = 5
            },
            Payments = new CommercialPaymentsDto
            {
                ByStatus = [facet],
                ByType = [new PaymentTypeAmountDto { Type = "DEPOSIT", Count = 1, Amount = 10m }],
                PaidAmountInRange = 10m,
                ExpiredCount = 1,
                CancelledCount = 2
            },
            Conversion = new CommercialConversionDto
            {
                ProjectsInCommercialBucket = 1, OrdersCreatedInRange = 2, DepositsPaidInRange = 3
            }
        };
        Assert.Equal(10m, commercial.Payments.ByType[0].Amount);

        var production = new ProductionReportDto
        {
            RequestsByStatus = [facet],
            ItemsByStatus = [facet],
            OpenRequestCount = 1,
            BlockedCount = 2,
            PendingReviewCount = 3,
            UnassignedCount = 4,
            OverdueCount = 5,
            CreatedInRange = 6,
            CompletedInRange = 7,
            TopAssignees =
            [
                new ProductionAssigneeLoadDto
                {
                    AccountId = Guid.NewGuid(), FullName = "A", OpenCount = 1, OverdueCount = 0
                }
            ]
        };
        Assert.Equal("A", production.TopAssignees[0].FullName);

        var delivery = new DeliveryReportDto
        {
            Projects = new DeliveryProjectsDto { ReadyForDelivery = 1, Delivering = 2, DeliveredInRange = 3 },
            Orders = new DeliveryOrdersDto { DeliveryRelatedByStatus = [facet], CustomerConfirmedInRange = 1 },
            OrderItems = new DeliveryOrderItemsDto { PartialDeliveryCount = 2 },
            Schedules = new DeliverySchedulesDto { UpcomingDeliveryOrHandover = 3, OverdueDeliveryOrHandover = 4 }
        };
        Assert.Equal(3, delivery.Projects.DeliveredInRange);

        var catalog = new CatalogReportDto
        {
            ProductsByStatus = [facet],
            CategoriesByStatus = [facet],
            BusinessTypesByStatus = [facet],
            VersionsByStatus = [facet],
            ProductsMissingActiveVersion = 1,
            ProductsMissing3D = 2,
            ProductsByCategory = [new NamedCountDto { Id = "1", Name = "Cat", Count = 3 }],
            ProductsByBusinessType = [new NamedCountDto { Id = "2", Name = "BT", Code = "CAFE", Count = 4 }]
        };
        Assert.Equal("CAFE", catalog.ProductsByBusinessType[0].Code);

        var overview = new ReportOverviewDto
        {
            Business = new ReportOverviewBusinessDto
            {
                TotalActiveAccounts = 1,
                DesignerAvailableCount = 1,
                DesignerFullCount = 0,
                DesignerOverCount = 0,
                SalesAvailableNowCount = 1,
                SalesFullNowCount = 0,
                SalesOverNowCount = 0,
                SalesHighFuturePressureCount = 1,
                UnassignedIntakeCount = 2
            },
            Projects = new ReportOverviewProjectsDto
            {
                TotalNonTerminal = 1,
                ByBucket = buckets,
                CompletedInRange = 1,
                RejectedInRange = 1
            },
            Commercial = new ReportOverviewCommercialDto
            {
                QuotationsSentInRange = 1,
                QuotationsAcceptedInRange = 2,
                OrdersOpen = 3,
                GmvInRange = 4m,
                CollectedInRange = 5m,
                OutstandingAmount = 6m
            },
            Production = new ReportOverviewProductionDto { RequestsOpen = 1, BlockedCount = 2, OverdueCount = 3 },
            Delivery = new ReportOverviewDeliveryDto
            {
                ReadyForDelivery = 1, Delivering = 2, DeliveredInRange = 3, UpcomingSchedules = 4
            },
            Catalog = new ReportOverviewCatalogDto
            {
                ActiveProducts = 1, ProductsMissingActiveVersion = 2, ProductsMissing3D = 3, ActiveBusinessTypes = 4
            }
        };
        Assert.Equal(4, overview.Catalog.ActiveBusinessTypes);

        var agingQuery = new ProjectAgingQueryDto
        {
            ThresholdDays = 14, Bucket = "INTAKE", Reason = "STUCK", Page = 2, PageSize = 10, SortBy = "AgeDaysDesc"
        };
        Assert.Equal(14, agingQuery.ThresholdDays);

        var agingItem = new ProjectAgingItemDto
        {
            ProjectId = Guid.NewGuid(),
            ProjectCode = "P",
            ProjectName = "N",
            Status = "SUBMITTED",
            Bucket = "INTAKE",
            Reason = "UNASSIGNED_INTAKE",
            SubmittedAt = DateTime.UtcNow,
            AgeDays = 9,
            CustomerId = Guid.NewGuid(),
            CustomerName = "C",
            AssignedSalesId = Guid.NewGuid(),
            SalesName = "S",
            AssignedDesignerId = Guid.NewGuid(),
            DesignerName = "D"
        };
        Assert.Equal(9, agingItem.AgeDays);

        var trend = new CommercialTrendDto
        {
            Granularity = "day",
            From = DateTime.UtcNow.Date,
            To = DateTime.UtcNow.Date,
            Points =
            [
                new CommercialTrendPointDto
                {
                    PeriodStart = DateTime.UtcNow.Date,
                    PeriodEnd = DateTime.UtcNow.Date,
                    QuotationsSent = 1,
                    QuotationsAccepted = 1,
                    OrdersCreated = 1,
                    Gmv = 2m,
                    Collected = 3m
                }
            ],
            Totals = new CommercialTrendTotalsDto
            {
                QuotationsSent = 1, QuotationsAccepted = 1, OrdersCreated = 1, Gmv = 2m, Collected = 3m
            }
        };
        Assert.Equal(1, trend.Points[0].OrdersCreated);

        var bestsellers = new CatalogBestsellersDto
        {
            Metric = "quantity",
            From = DateTime.UtcNow.Date,
            To = DateTime.UtcNow.Date,
            Items =
            [
                new CatalogBestsellerItemDto
                {
                    ProductId = Guid.NewGuid(),
                    ProductName = "Chair",
                    ProductVersionId = Guid.NewGuid(),
                    Sku = "SKU",
                    QuantitySold = 5,
                    Revenue = 100m
                }
            ]
        };
        Assert.Equal("SKU", bestsellers.Items[0].Sku);

        var reviews = new DeliveryReviewsDto
        {
            Summary = new DeliveryReviewsSummaryDto
            {
                ReviewCount = 1, AverageOverallRating = 4.5, AverageDeliveryRating = 4.0
            },
            Items =
            [
                new DeliveryReviewItemDto
                {
                    ProjectId = Guid.NewGuid(),
                    ProjectCode = "P",
                    OverallRating = 5,
                    DeliveryRating = 4,
                    Comment = "ok",
                    CreatedAt = DateTime.UtcNow
                }
            ],
            Page = 1,
            PageSize = 20,
            TotalItems = 1,
            TotalPages = 1,
            HasPreviousPage = false,
            HasNextPage = false
        };
        Assert.Equal(4.5, reviews.Summary.AverageOverallRating);

        var workloadItem = new ProductionWorkloadItemDto
        {
            AccountId = Guid.NewGuid(),
            FullName = "P",
            Email = "p@x.com",
            OpenRequestCount = 1,
            BlockedCount = 0,
            OverdueCount = 0,
            MaxActiveRequests = 5,
            AvailableSlot = 4,
            CapacityState = "AVAILABLE"
        };
        var workloadSummary = new ProductionWorkloadSummaryDto
        {
            TotalActiveStaff = 1,
            AvailableCount = 1,
            FullCount = 0,
            OverCount = 0,
            TotalOpenRequests = 1,
            BlockedCount = 0,
            OverdueCount = 0,
            MaxActiveRequests = 5
        };
        var workloadQuery = new ProductionWorkloadQueryDto
        {
            Page = 1, PageSize = 20, Search = "a", CapacityState = "AVAILABLE", SortBy = "OpenRequestCountDesc"
        };
        Assert.Equal("AVAILABLE", workloadItem.CapacityState);
        Assert.Equal(5, workloadSummary.MaxActiveRequests);
        Assert.Equal("a", workloadQuery.Search);

        var exportQuery = new ReportExportQueryDto { Domain = "business", Format = "csv", From = DateTime.UtcNow, To = DateTime.UtcNow };
        var exportFile = new ReportExportFileDto { FileName = "a.csv", ContentType = "text/csv", Content = [1] };
        Assert.Equal("business", exportQuery.Domain);
        Assert.Equal("a.csv", exportFile.FileName);

        var trendQuery = new CommercialTrendQueryDto { From = DateTime.UtcNow, To = DateTime.UtcNow, Granularity = "day" };
        var bestQuery = new CatalogBestsellersQueryDto { From = DateTime.UtcNow, To = DateTime.UtcNow, Metric = "quantity", Limit = 20 };
        var reviewQuery = new DeliveryReviewsQueryDto { From = DateTime.UtcNow, To = DateTime.UtcNow, Page = 1, PageSize = 20 };
        Assert.Equal("day", trendQuery.Granularity);
        Assert.Equal(20, bestQuery.Limit);
        Assert.Equal(1, reviewQuery.Page);
    }
}
