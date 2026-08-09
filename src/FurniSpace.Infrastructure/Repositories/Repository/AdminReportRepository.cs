using FurniSpace.Domain.Enums;
using FurniSpace.Shared.DTOs.Reports;
using FurniSpace.Infrastructure.Common.Accounts;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Infrastructure.Repositories.Repository;

public sealed class AdminReportRepository : IAdminReportRepository
{
    private static readonly OrderStatus[] OpenOrderStatuses =
    [
        OrderStatus.CREATED,
        OrderStatus.DEPOSIT_PENDING,
        OrderStatus.DEPOSIT_PAID,
        OrderStatus.IN_PRODUCTION,
        OrderStatus.READY_FOR_DELIVERY,
        OrderStatus.DELIVERING,
        OrderStatus.DELIVERED,
        OrderStatus.FINAL_PAYMENT_PENDING
    ];

    private static readonly OrderStatus[] DeliveryRelatedOrderStatuses =
    [
        OrderStatus.READY_FOR_DELIVERY,
        OrderStatus.DELIVERING,
        OrderStatus.DELIVERED,
        OrderStatus.FINAL_PAYMENT_PENDING
    ];

    private static readonly ProductionRequestStatus[] OpenProductionStatuses =
    [
        ProductionRequestStatus.PENDING_REVIEW,
        ProductionRequestStatus.FEASIBLE,
        ProductionRequestStatus.IN_PRODUCTION,
        ProductionRequestStatus.BLOCKED
    ];

    private readonly AppDbContext _db;

    public AdminReportRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<(string Key, long Count)>> CountAccountsByStatusAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.AccountSet.AsNoTracking()
            .Where(account => account.DeletedAt == null)
            .GroupBy(account => account.Status)
            .Select(group => new { Key = group.Key, Count = group.LongCount() })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => (row.Key?.ToString() ?? "UNKNOWN", row.Count))
            .ToList();
    }

    public async Task<IReadOnlyList<(string Key, long Count, string? Label)>> CountAccountsByRoleAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await (
            from account in _db.AccountSet.AsNoTracking()
            join role in _db.RoleSet.AsNoTracking() on account.RoleId equals role.RoleId
            where account.DeletedAt == null
            group account by role.RoleName into groupBy
            select new
            {
                Key = groupBy.Key,
                Count = groupBy.LongCount()
            })
            .ToListAsync(cancellationToken);

        return rows.Select(row => (row.Key, row.Count, (string?)row.Key)).ToList();
    }

    public async Task<ProjectReportDto> GetProjectReportAsync(
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default)
    {
        var projects = await _db.ProjectSet.AsNoTracking().ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;

        var byStatus = projects
            .GroupBy(project => project.Status?.ToString() ?? "UNKNOWN")
            .Select(group => new ReportFacetCountDto { Key = group.Key, Count = group.Count() })
            .OrderBy(item => item.Key)
            .ToList();

        var buckets = new ProjectBucketCountsDto();
        foreach (var project in projects)
        {
            switch (SalesWorkloadPressurePolicy.ResolveBucket(project.Status))
            {
                case SalesWorkloadPressurePolicy.BucketIntake:
                    buckets.Intake++;
                    break;
                case SalesWorkloadPressurePolicy.BucketCommercial:
                    buckets.Commercial++;
                    break;
                case SalesWorkloadPressurePolicy.BucketDesignMonitor:
                    buckets.DesignMonitor++;
                    break;
                case SalesWorkloadPressurePolicy.BucketFulfillment:
                    buckets.Fulfillment++;
                    break;
                case SalesWorkloadPressurePolicy.BucketTerminal:
                    buckets.Terminal++;
                    break;
                default:
                    buckets.Other++;
                    break;
            }
        }

        var nonTerminal = projects
            .Where(project =>
                project.Status != ProjectStatus.COMPLETED &&
                project.Status != ProjectStatus.REJECTED)
            .ToList();

        int AgeDays(Domain.Entities.Project project)
        {
            var baseAt = project.SubmittedAt ?? project.CreatedAt ?? now;
            return Math.Max(0, (int)(now.Date - baseAt.Date).TotalDays);
        }

        return new ProjectReportDto
        {
            ByStatus = byStatus,
            ByBucket = buckets,
            UnassignedIntakeCount = projects.Count(project =>
                project.Status == ProjectStatus.SUBMITTED &&
                project.AssignedSalesId == null),
            WaitingForDesignerCount = projects.Count(project =>
                project.Status == ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT),
            CompletedInRange = projects.Count(project =>
                project.Status == ProjectStatus.COMPLETED &&
                InRange(project.CompletedAt, from, to)),
            RejectedInRange = projects.Count(project =>
                project.Status == ProjectStatus.REJECTED &&
                InRange(project.RejectedAt, from, to)),
            TotalNonTerminal = nonTerminal.Count,
            Aging = new ProjectAgingCountsDto
            {
                Over7Days = nonTerminal.Count(project => AgeDays(project) >= 7),
                Over14Days = nonTerminal.Count(project => AgeDays(project) >= 14),
                Over30Days = nonTerminal.Count(project => AgeDays(project) >= 30)
            }
        };
    }

    public async Task<CommercialReportDto> GetCommercialReportAsync(
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default)
    {
        var quotations = await _db.QuotationSet.AsNoTracking().ToListAsync(cancellationToken);
        var orders = await _db.OrderSet.AsNoTracking().ToListAsync(cancellationToken);
        var payments = await _db.PaymentSet.AsNoTracking().ToListAsync(cancellationToken);
        var projects = await _db.ProjectSet.AsNoTracking().ToListAsync(cancellationToken);

        var openOrders = orders.Where(order =>
            order.Status.HasValue &&
            OpenOrderStatuses.Contains(order.Status.Value)).ToList();

        return new CommercialReportDto
        {
            Quotations = new CommercialQuotationsDto
            {
                ByStatus = quotations
                    .GroupBy(item => item.Status?.ToString() ?? "UNKNOWN")
                    .Select(group => new ReportFacetCountDto { Key = group.Key, Count = group.Count() })
                    .OrderBy(item => item.Key)
                    .ToList(),
                SentInRange = quotations.Count(item => InRange(item.SentAt, from, to)),
                AcceptedInRange = quotations.Count(item => InRange(item.AcceptedAt, from, to)),
                RevisionRequestedCount = quotations.Count(item => item.Status == QuotationStatus.REVISION_REQUESTED),
                RevisedCount = quotations.Count(item => item.Status == QuotationStatus.REVISED)
            },
            Orders = new CommercialOrdersDto
            {
                ByStatus = orders
                    .GroupBy(item => item.Status?.ToString() ?? "UNKNOWN")
                    .Select(group => new ReportFacetCountDto { Key = group.Key, Count = group.Count() })
                    .OrderBy(item => item.Key)
                    .ToList(),
                OpenCount = openOrders.Count,
                GmvInRange = orders
                    .Where(order => InRange(order.CreatedAt, from, to))
                    .Sum(order => order.FinalTotalAmount),
                CollectedTotal = orders.Sum(order => order.PaidAmount ?? 0m),
                OutstandingAmount = openOrders.Sum(order => order.RemainingAmount ?? 0m),
                CreatedInRange = orders.Count(order => InRange(order.CreatedAt, from, to))
            },
            Payments = new CommercialPaymentsDto
            {
                ByStatus = payments
                    .GroupBy(item => item.Status?.ToString() ?? "UNKNOWN")
                    .Select(group => new ReportFacetCountDto { Key = group.Key, Count = group.Count() })
                    .OrderBy(item => item.Key)
                    .ToList(),
                ByType = payments
                    .GroupBy(item => item.PaymentType?.ToString() ?? "UNKNOWN")
                    .Select(group => new PaymentTypeAmountDto
                    {
                        Type = group.Key,
                        Count = group.Count(),
                        Amount = group.Sum(item => item.Amount)
                    })
                    .OrderBy(item => item.Type)
                    .ToList(),
                PaidAmountInRange = payments
                    .Where(item => item.Status == PaymentStatus.PAID && InRange(item.PaidAt, from, to))
                    .Sum(item => item.Amount),
                ExpiredCount = payments.Count(item => item.Status == PaymentStatus.EXPIRED),
                CancelledCount = payments.Count(item => item.Status == PaymentStatus.CANCELLED)
            },
            Conversion = new CommercialConversionDto
            {
                ProjectsInCommercialBucket = projects.Count(project =>
                    SalesWorkloadPressurePolicy.ResolveBucket(project.Status) ==
                    SalesWorkloadPressurePolicy.BucketCommercial),
                OrdersCreatedInRange = orders.Count(order => InRange(order.CreatedAt, from, to)),
                DepositsPaidInRange = payments.Count(payment =>
                    payment.Status == PaymentStatus.PAID &&
                    payment.PaymentType == PaymentType.DEPOSIT &&
                    InRange(payment.PaidAt, from, to))
            }
        };
    }

    public async Task<ProductionReportDto> GetProductionReportAsync(
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default)
    {
        var requests = await _db.ProductionRequestSet.AsNoTracking().ToListAsync(cancellationToken);
        var items = await _db.ProductionItemSet.AsNoTracking().ToListAsync(cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var open = requests.Where(request =>
            request.Status.HasValue &&
            OpenProductionStatuses.Contains(request.Status.Value)).ToList();

        bool IsOverdue(Domain.Entities.ProductionRequest request) =>
            request.EstimatedCompletionDate.HasValue &&
            request.EstimatedCompletionDate.Value < today &&
            request.Status != ProductionRequestStatus.COMPLETED &&
            request.Status != ProductionRequestStatus.CANCELLED;

        var assigneeIds = open
            .Where(request => request.AssignedTo.HasValue)
            .Select(request => request.AssignedTo!.Value)
            .Distinct()
            .ToList();

        var accounts = await _db.AccountSet.AsNoTracking()
            .Where(account => assigneeIds.Contains(account.AccountId))
            .ToDictionaryAsync(account => account.AccountId, cancellationToken);

        var topAssignees = open
            .Where(request => request.AssignedTo.HasValue)
            .GroupBy(request => request.AssignedTo!.Value)
            .Select(group =>
            {
                accounts.TryGetValue(group.Key, out var account);
                return new ProductionAssigneeLoadDto
                {
                    AccountId = group.Key,
                    FullName = account?.FullName ?? string.Empty,
                    OpenCount = group.Count(),
                    OverdueCount = group.Count(IsOverdue)
                };
            })
            .OrderByDescending(item => item.OpenCount)
            .Take(10)
            .ToList();

        return new ProductionReportDto
        {
            RequestsByStatus = requests
                .GroupBy(item => item.Status?.ToString() ?? "UNKNOWN")
                .Select(group => new ReportFacetCountDto { Key = group.Key, Count = group.Count() })
                .OrderBy(item => item.Key)
                .ToList(),
            ItemsByStatus = items
                .GroupBy(item => item.Status?.ToString() ?? "UNKNOWN")
                .Select(group => new ReportFacetCountDto { Key = group.Key, Count = group.Count() })
                .OrderBy(item => item.Key)
                .ToList(),
            OpenRequestCount = open.Count,
            BlockedCount = requests.Count(item => item.Status == ProductionRequestStatus.BLOCKED),
            PendingReviewCount = requests.Count(item => item.Status == ProductionRequestStatus.PENDING_REVIEW),
            UnassignedCount = open.Count(item => item.AssignedTo == null),
            OverdueCount = requests.Count(IsOverdue),
            CreatedInRange = requests.Count(item => InRange(item.CreatedAt, from, to)),
            CompletedInRange = requests.Count(item =>
                item.Status == ProductionRequestStatus.COMPLETED &&
                InRange(item.UpdatedAt, from, to)),
            TopAssignees = topAssignees
        };
    }

    public async Task<DeliveryReportDto> GetDeliveryReportAsync(
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default)
    {
        var projects = await _db.ProjectSet.AsNoTracking().ToListAsync(cancellationToken);
        var orders = await _db.OrderSet.AsNoTracking().ToListAsync(cancellationToken);
        var orderItems = await _db.OrderItemSet.AsNoTracking().ToListAsync(cancellationToken);
        var schedules = await _db.ProjectScheduleSet.AsNoTracking().ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;

        var deliveryOrders = orders.Where(order =>
            order.Status.HasValue &&
            DeliveryRelatedOrderStatuses.Contains(order.Status.Value)).ToList();

        var deliverySchedules = schedules.Where(schedule =>
            schedule.ScheduleType is ProjectScheduleType.DELIVERY or ProjectScheduleType.HANDOVER &&
            schedule.Status != ProjectScheduleStatus.CANCELLED &&
            schedule.Status != ProjectScheduleStatus.COMPLETED).ToList();

        return new DeliveryReportDto
        {
            Projects = new DeliveryProjectsDto
            {
                ReadyForDelivery = projects.Count(project => project.Status == ProjectStatus.READY_FOR_DELIVERY),
                Delivering = projects.Count(project => project.Status == ProjectStatus.DELIVERING),
                DeliveredInRange = projects.Count(project =>
                    project.Status == ProjectStatus.DELIVERED &&
                    InRange(project.UpdatedAt ?? project.CompletedAt, from, to))
            },
            Orders = new DeliveryOrdersDto
            {
                DeliveryRelatedByStatus = deliveryOrders
                    .GroupBy(order => order.Status?.ToString() ?? "UNKNOWN")
                    .Select(group => new ReportFacetCountDto { Key = group.Key, Count = group.Count() })
                    .OrderBy(item => item.Key)
                    .ToList(),
                CustomerConfirmedInRange = orderItems.Count(item => InRange(item.CustomerConfirmedAt, from, to))
            },
            OrderItems = new DeliveryOrderItemsDto
            {
                PartialDeliveryCount = orderItems.Count(item =>
                    (item.DeliveredQuantity ?? 0) > 0 &&
                    (item.Quantity ?? 0) > (item.DeliveredQuantity ?? 0))
            },
            Schedules = new DeliverySchedulesDto
            {
                UpcomingDeliveryOrHandover = deliverySchedules.Count(schedule =>
                    schedule.ScheduledStart >= now),
                OverdueDeliveryOrHandover = deliverySchedules.Count(schedule =>
                    schedule.ScheduledEnd.HasValue &&
                    schedule.ScheduledEnd.Value < now)
            }
        };
    }

    public async Task<CatalogReportDto> GetCatalogReportAsync(CancellationToken cancellationToken = default)
    {
        var products = await _db.ProductSet.AsNoTracking().ToListAsync(cancellationToken);
        var categories = await _db.CategorySet.AsNoTracking().ToListAsync(cancellationToken);
        var businessTypes = await _db.BusinessTypeSet.AsNoTracking().ToListAsync(cancellationToken);
        var versions = await _db.ProductVersionSet.AsNoTracking().ToListAsync(cancellationToken);

        var activeVersionProductIds = versions
            .Where(version => version.Status == ProductStatus.ACTIVE)
            .Select(version => version.ProductId)
            .ToHashSet();

        var productRefs = await _db.FileLinkSet.AsNoTracking()
            .Where(link =>
                link.FileType == FileType.MODEL_3D &&
                link.ReferenceType == "PRODUCT")
            .Select(link => link.ReferenceId)
            .ToListAsync(cancellationToken);

        var versionRefs = await (
            from link in _db.FileLinkSet.AsNoTracking()
            join version in _db.ProductVersionSet.AsNoTracking() on link.ReferenceId equals version.ProductVersionId
            where link.FileType == FileType.MODEL_3D && link.ReferenceType == "PRODUCT_VERSION"
            select version.ProductId)
            .ToListAsync(cancellationToken);

        var productIdsWith3D = productRefs.Concat(versionRefs).ToHashSet();

        var byCategory = (
            from product in products
            join category in categories on product.CategoryId equals category.CategoryId into categoryJoin
            from category in categoryJoin.DefaultIfEmpty()
            group product by new
            {
                Id = product.CategoryId?.ToString() ?? "NONE",
                Name = category?.CategoryName ?? "Uncategorized"
            }
            into groupBy
            select new NamedCountDto
            {
                Id = groupBy.Key.Id,
                Name = groupBy.Key.Name,
                Count = groupBy.Count()
            })
            .OrderByDescending(item => item.Count)
            .Take(20)
            .ToList();

        var byBusinessType = businessTypes
            .Select(bt => new NamedCountDto
            {
                Id = bt.Id.ToString(),
                Name = bt.Name,
                Code = bt.Code,
                Count = products.Count(product =>
                    product.BusinessTypeIds != null &&
                    product.BusinessTypeIds.Contains(bt.Id))
            })
            .Where(item => item.Count > 0)
            .OrderByDescending(item => item.Count)
            .Take(20)
            .ToList();

        return new CatalogReportDto
        {
            ProductsByStatus = products
                .GroupBy(item => item.Status?.ToString() ?? "UNKNOWN")
                .Select(group => new ReportFacetCountDto { Key = group.Key, Count = group.Count() })
                .OrderBy(item => item.Key)
                .ToList(),
            CategoriesByStatus = categories
                .GroupBy(item => item.Status?.ToString() ?? "UNKNOWN")
                .Select(group => new ReportFacetCountDto { Key = group.Key, Count = group.Count() })
                .OrderBy(item => item.Key)
                .ToList(),
            BusinessTypesByStatus =
            [
                new ReportFacetCountDto { Key = "ACTIVE", Count = businessTypes.Count(item => item.Status) },
                new ReportFacetCountDto { Key = "INACTIVE", Count = businessTypes.Count(item => !item.Status) }
            ],
            VersionsByStatus = versions
                .GroupBy(item => item.Status?.ToString() ?? "UNKNOWN")
                .Select(group => new ReportFacetCountDto { Key = group.Key, Count = group.Count() })
                .OrderBy(item => item.Key)
                .ToList(),
            ProductsMissingActiveVersion = products.Count(product => !activeVersionProductIds.Contains(product.ProductId)),
            ProductsMissing3D = products.Count(product => !productIdsWith3D.Contains(product.ProductId)),
            ProductsByCategory = byCategory,
            ProductsByBusinessType = byBusinessType
        };
    }

    public async Task<(IReadOnlyList<ProjectAgingItemDto> Items, int Total)> GetProjectAgingAsync(
        int thresholdDays,
        string? bucket,
        string? reason,
        int page,
        int pageSize,
        string sortBy,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var projects = await _db.ProjectSet.AsNoTracking().ToListAsync(cancellationToken);
        var customerIds = projects.Select(project => project.CustomerId).Distinct().ToList();
        var salesIds = projects.Where(project => project.AssignedSalesId.HasValue)
            .Select(project => project.AssignedSalesId!.Value).Distinct().ToList();
        var designerIds = projects.Where(project => project.AssignedDesignerId.HasValue)
            .Select(project => project.AssignedDesignerId!.Value).Distinct().ToList();

        var accountIds = customerIds.Concat(salesIds).Concat(designerIds).Distinct().ToList();
        var accounts = await _db.AccountSet.AsNoTracking()
            .Where(account => accountIds.Contains(account.AccountId))
            .ToDictionaryAsync(account => account.AccountId, cancellationToken);

        var items = projects
            .Where(project =>
                project.Status != ProjectStatus.COMPLETED &&
                project.Status != ProjectStatus.REJECTED)
            .Select(project =>
            {
                var baseAt = project.SubmittedAt ?? project.CreatedAt ?? now;
                var ageDays = Math.Max(0, (int)(now.Date - baseAt.Date).TotalDays);
                var resolvedBucket = SalesWorkloadPressurePolicy.ResolveBucket(project.Status);
                var resolvedReason =
                    project.Status == ProjectStatus.SUBMITTED && project.AssignedSalesId == null
                        ? "UNASSIGNED_INTAKE"
                        : project.Status == ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT
                            ? "WAITING_DESIGNER"
                            : ageDays >= thresholdDays
                                ? "STUCK"
                                : "OTHER";

                accounts.TryGetValue(project.CustomerId, out var customer);
                Domain.Entities.Account? sales = null;
                Domain.Entities.Account? designer = null;
                if (project.AssignedSalesId.HasValue)
                {
                    accounts.TryGetValue(project.AssignedSalesId.Value, out sales);
                }

                if (project.AssignedDesignerId.HasValue)
                {
                    accounts.TryGetValue(project.AssignedDesignerId.Value, out designer);
                }

                return new ProjectAgingItemDto
                {
                    ProjectId = project.ProjectId,
                    ProjectCode = project.ProjectCode,
                    ProjectName = project.ProjectName,
                    Status = project.Status?.ToString(),
                    Bucket = resolvedBucket,
                    Reason = resolvedReason,
                    SubmittedAt = project.SubmittedAt ?? project.CreatedAt,
                    AgeDays = ageDays,
                    CustomerId = project.CustomerId,
                    CustomerName = customer?.FullName,
                    AssignedSalesId = project.AssignedSalesId,
                    SalesName = sales?.FullName,
                    AssignedDesignerId = project.AssignedDesignerId,
                    DesignerName = designer?.FullName
                };
            })
            .Where(item => item.AgeDays >= thresholdDays ||
                           item.Reason is "UNASSIGNED_INTAKE" or "WAITING_DESIGNER")
            .AsEnumerable();

        if (!string.IsNullOrWhiteSpace(bucket))
        {
            items = items.Where(item =>
                string.Equals(item.Bucket, bucket, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(reason))
        {
            items = items.Where(item =>
                string.Equals(item.Reason, reason, StringComparison.OrdinalIgnoreCase));
        }

        items = string.Equals(sortBy, "SubmittedAtAsc", StringComparison.OrdinalIgnoreCase)
            ? items.OrderBy(item => item.SubmittedAt).ThenBy(item => item.ProjectCode)
            : items.OrderByDescending(item => item.AgeDays).ThenBy(item => item.ProjectCode);

        var list = items.ToList();
        var total = list.Count;
        var pageItems = list.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return (pageItems, total);
    }

    public async Task<CommercialTrendDto> GetCommercialTrendAsync(
        DateTime from,
        DateTime to,
        string granularity,
        CancellationToken cancellationToken = default)
    {
        var quotations = await _db.QuotationSet.AsNoTracking().ToListAsync(cancellationToken);
        var orders = await _db.OrderSet.AsNoTracking().ToListAsync(cancellationToken);
        var payments = await _db.PaymentSet.AsNoTracking().ToListAsync(cancellationToken);

        var isWeek = string.Equals(granularity, "week", StringComparison.OrdinalIgnoreCase);
        var points = new List<CommercialTrendPointDto>();
        var cursor = from.Date;
        var end = to.Date;

        while (cursor <= end)
        {
            var periodEnd = isWeek
                ? cursor.AddDays(6)
                : cursor;
            if (periodEnd > end)
            {
                periodEnd = end;
            }

            var periodFrom = cursor;
            var periodToExclusive = periodEnd.AddDays(1);

            points.Add(new CommercialTrendPointDto
            {
                PeriodStart = periodFrom,
                PeriodEnd = periodEnd,
                QuotationsSent = quotations.Count(item =>
                    item.SentAt.HasValue &&
                    item.SentAt.Value >= periodFrom &&
                    item.SentAt.Value < periodToExclusive),
                QuotationsAccepted = quotations.Count(item =>
                    item.AcceptedAt.HasValue &&
                    item.AcceptedAt.Value >= periodFrom &&
                    item.AcceptedAt.Value < periodToExclusive),
                OrdersCreated = orders.Count(item =>
                    item.CreatedAt.HasValue &&
                    item.CreatedAt.Value >= periodFrom &&
                    item.CreatedAt.Value < periodToExclusive),
                Gmv = orders
                    .Where(item =>
                        item.CreatedAt.HasValue &&
                        item.CreatedAt.Value >= periodFrom &&
                        item.CreatedAt.Value < periodToExclusive)
                    .Sum(item => item.FinalTotalAmount),
                Collected = payments
                    .Where(item =>
                        item.Status == PaymentStatus.PAID &&
                        item.PaidAt.HasValue &&
                        item.PaidAt.Value >= periodFrom &&
                        item.PaidAt.Value < periodToExclusive)
                    .Sum(item => item.Amount)
            });

            cursor = isWeek ? cursor.AddDays(7) : cursor.AddDays(1);
        }

        return new CommercialTrendDto
        {
            Granularity = isWeek ? "week" : "day",
            From = from.Date,
            To = to.Date,
            Points = points,
            Totals = new CommercialTrendTotalsDto
            {
                QuotationsSent = points.Sum(point => point.QuotationsSent),
                QuotationsAccepted = points.Sum(point => point.QuotationsAccepted),
                OrdersCreated = points.Sum(point => point.OrdersCreated),
                Gmv = points.Sum(point => point.Gmv),
                Collected = points.Sum(point => point.Collected)
            }
        };
    }

    public async Task<CatalogBestsellersDto> GetCatalogBestsellersAsync(
        DateTime from,
        DateTime to,
        string metric,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var ordersInRange = await _db.OrderSet.AsNoTracking()
            .Where(order => order.CreatedAt != null && order.CreatedAt >= from && order.CreatedAt <= to)
            .Select(order => order.OrderId)
            .ToListAsync(cancellationToken);

        var items = await _db.OrderItemSet.AsNoTracking()
            .Where(item => ordersInRange.Contains(item.OrderId))
            .ToListAsync(cancellationToken);

        var grouped = items
            .GroupBy(item => new
            {
                item.ProductVersionId,
                Name = item.ProductNameSnapshot ?? "Unknown",
                Sku = item.ProductVersionCodeSnapshot
            })
            .Select(group => new CatalogBestsellerItemDto
            {
                ProductVersionId = group.Key.ProductVersionId,
                ProductName = group.Key.Name,
                Sku = group.Key.Sku,
                QuantitySold = group.Sum(item => item.Quantity ?? 0),
                Revenue = group.Sum(item => item.TotalAmount ?? item.SubtotalAmount ?? 0m)
            });

        var byMetric = string.Equals(metric, "revenue", StringComparison.OrdinalIgnoreCase)
            ? grouped.OrderByDescending(item => item.Revenue)
            : grouped.OrderByDescending(item => item.QuantitySold);

        var top = byMetric.Take(limit).ToList();

        var versionIds = top
            .Where(item => item.ProductVersionId.HasValue)
            .Select(item => item.ProductVersionId!.Value)
            .ToList();

        var versionMap = await _db.ProductVersionSet.AsNoTracking()
            .Where(version => versionIds.Contains(version.ProductVersionId))
            .ToDictionaryAsync(version => version.ProductVersionId, cancellationToken);

        foreach (var item in top)
        {
            if (item.ProductVersionId.HasValue &&
                versionMap.TryGetValue(item.ProductVersionId.Value, out var version))
            {
                item.ProductId = version.ProductId;
            }
        }

        return new CatalogBestsellersDto
        {
            Metric = string.Equals(metric, "revenue", StringComparison.OrdinalIgnoreCase) ? "revenue" : "quantity",
            From = from,
            To = to,
            Items = top
        };
    }

    public async Task<DeliveryReviewsDto> GetDeliveryReviewsAsync(
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _db.ProjectReviewSet.AsNoTracking().AsQueryable();
        if (from.HasValue)
        {
            query = query.Where(review => review.CreatedAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(review => review.CreatedAt <= to.Value);
        }

        var reviews = await query.ToListAsync(cancellationToken);
        var total = reviews.Count;
        var pageItems = reviews
            .OrderByDescending(review => review.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var projectIds = pageItems.Select(review => review.ProjectId).Distinct().ToList();
        var projects = await _db.ProjectSet.AsNoTracking()
            .Where(project => projectIds.Contains(project.ProjectId))
            .ToDictionaryAsync(project => project.ProjectId, cancellationToken);

        var totalPages = pageSize == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);

        return new DeliveryReviewsDto
        {
            Summary = new DeliveryReviewsSummaryDto
            {
                ReviewCount = total,
                AverageOverallRating = reviews.Count == 0
                    ? null
                    : reviews.Where(review => review.Rating.HasValue).Select(review => (double)review.Rating!.Value).DefaultIfEmpty().Average(),
                AverageDeliveryRating = reviews.Count == 0
                    ? null
                    : reviews.Where(review => review.DeliveryRating.HasValue).Select(review => (double)review.DeliveryRating!.Value).DefaultIfEmpty().Average()
            },
            Items = pageItems.Select(review =>
            {
                projects.TryGetValue(review.ProjectId, out var project);
                return new DeliveryReviewItemDto
                {
                    ProjectId = review.ProjectId,
                    ProjectCode = project?.ProjectCode,
                    OverallRating = review.Rating,
                    DeliveryRating = review.DeliveryRating,
                    Comment = review.Comment,
                    CreatedAt = review.CreatedAt
                };
            }).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
            TotalPages = totalPages,
            HasPreviousPage = page > 1,
            HasNextPage = page < totalPages
        };
    }

    public async Task<(IReadOnlyList<ProductionWorkloadItemDto> Items, int Total, ProductionWorkloadSummaryDto Summary)> GetProductionWorkloadAsync(
        int page,
        int pageSize,
        int maxActiveRequests,
        string? search,
        string? capacityState,
        string sortBy,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var productionRoleIds = await _db.RoleSet.AsNoTracking()
            .Where(role => role.RoleName == "PRODUCTION" || role.RoleName == "PRODUCTION_STAFF")
            .Select(role => role.RoleId)
            .ToListAsync(cancellationToken);

        var staff = await _db.AccountSet.AsNoTracking()
            .Where(account =>
                account.DeletedAt == null &&
                account.Status == AccountStatus.ACTIVE &&
                productionRoleIds.Contains(account.RoleId))
            .ToListAsync(cancellationToken);

        var requests = await _db.ProductionRequestSet.AsNoTracking().ToListAsync(cancellationToken);

        bool IsOpen(Domain.Entities.ProductionRequest request) =>
            request.Status.HasValue && OpenProductionStatuses.Contains(request.Status.Value);

        bool IsOverdue(Domain.Entities.ProductionRequest request) =>
            request.EstimatedCompletionDate.HasValue &&
            request.EstimatedCompletionDate.Value < today &&
            request.Status != ProductionRequestStatus.COMPLETED &&
            request.Status != ProductionRequestStatus.CANCELLED;

        var items = staff.Select(account =>
        {
            var assigned = requests.Where(request => request.AssignedTo == account.AccountId).ToList();
            var openCount = assigned.Count(IsOpen);
            var blockedCount = assigned.Count(request => request.Status == ProductionRequestStatus.BLOCKED);
            var overdueCount = assigned.Count(IsOverdue);
            var availableSlot = maxActiveRequests - openCount;
            var state = openCount < maxActiveRequests
                ? "AVAILABLE"
                : openCount == maxActiveRequests
                    ? "FULL"
                    : "OVER";

            return new ProductionWorkloadItemDto
            {
                AccountId = account.AccountId,
                FullName = account.FullName,
                Email = account.Email,
                OpenRequestCount = openCount,
                BlockedCount = blockedCount,
                OverdueCount = overdueCount,
                MaxActiveRequests = maxActiveRequests,
                AvailableSlot = availableSlot,
                CapacityState = state
            };
        }).ToList();

        var summary = new ProductionWorkloadSummaryDto
        {
            TotalActiveStaff = items.Count,
            AvailableCount = items.Count(item => item.CapacityState == "AVAILABLE"),
            FullCount = items.Count(item => item.CapacityState == "FULL"),
            OverCount = items.Count(item => item.CapacityState == "OVER"),
            TotalOpenRequests = items.Sum(item => item.OpenRequestCount),
            BlockedCount = items.Sum(item => item.BlockedCount),
            OverdueCount = items.Sum(item => item.OverdueCount),
            MaxActiveRequests = maxActiveRequests
        };

        IEnumerable<ProductionWorkloadItemDto> filtered = items;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            filtered = filtered.Where(item =>
                item.FullName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                item.Email.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(capacityState))
        {
            filtered = filtered.Where(item =>
                string.Equals(item.CapacityState, capacityState, StringComparison.OrdinalIgnoreCase));
        }

        filtered = string.Equals(sortBy, "AvailableSlotDesc", StringComparison.OrdinalIgnoreCase)
            ? filtered.OrderByDescending(item => item.AvailableSlot).ThenBy(item => item.FullName)
            : filtered.OrderByDescending(item => item.OpenRequestCount).ThenBy(item => item.FullName);

        var list = filtered.ToList();
        var pageItems = list.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return (pageItems, list.Count, summary);
    }

    private static bool InRange(DateTime? value, DateTime? from, DateTime? to)
    {
        if (!value.HasValue)
        {
            return false;
        }

        if (from.HasValue && value.Value < from.Value)
        {
            return false;
        }

        if (to.HasValue && value.Value > to.Value)
        {
            return false;
        }

        return true;
    }
}
