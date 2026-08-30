#nullable enable

using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.ReadModels.Dashboard;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Infrastructure.Repositories.Repository;

public sealed class DashboardQueueReadRepository : IDashboardQueueReadRepository
{
    private static readonly OrderStatus[] NonCancelledOrderStatuses =
    [
        OrderStatus.CREATED,
        OrderStatus.DEPOSIT_PENDING,
        OrderStatus.DEPOSIT_PAID,
        OrderStatus.IN_PRODUCTION,
        OrderStatus.READY_FOR_DELIVERY,
        OrderStatus.DELIVERING,
        OrderStatus.DELIVERED,
        OrderStatus.FINAL_PAYMENT_PENDING,
        OrderStatus.COMPLETED
    ];

    private static readonly ProjectStatus[] DesignerQueueStatuses =
    [
        ProjectStatus.MEASUREMENT_REQUIRED,
        ProjectStatus.SPACE_VERIFIED,
        ProjectStatus.PROPOSAL_CONSULTING,
        ProjectStatus.PROPOSAL_SELECTED,
        ProjectStatus.QUOTATION_REVISION_REQUESTED
    ];

    private static readonly ProductionRequestStatus[] ActiveProductionStatuses =
    [
        ProductionRequestStatus.PENDING,
        ProductionRequestStatus.IN_PRODUCTION
    ];

    private static readonly OrderStatus[] DeliveryActiveOrderStatuses =
    [
        OrderStatus.READY_FOR_DELIVERY,
        OrderStatus.DELIVERING,
        OrderStatus.AWAITING_CUSTOMER_CONFIRMATION
    ];

    private static readonly ProductionItemStatus[] TerminalProductionItemStatuses =
    [
        ProductionItemStatus.COMPLETED,
        ProductionItemStatus.CANCELLED
    ];

    private readonly AppDbContext _db;

    public DashboardQueueReadRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<DashboardProjectQueueRowReadModel>> GetSalesQueueRowsAsync(
        DashboardQueueFilterReadModel filter,
        CancellationToken cancellationToken = default)
    {
        var query = BuildSalesProjectQuery(filter);
        return await ProjectToQueueRows(query).ToListAsync(cancellationToken);
    }

    public async Task<SalesDashboardKpisReadModel> GetSalesKpisAsync(
        DashboardQueueFilterReadModel filter,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(filter.UtcNow);
        var projects = BuildSalesProjectQuery(filter);

        var newRequests = await projects.CountAsync(
            project => project.Status == ProjectStatus.SUBMITTED,
            cancellationToken);

        var waitingCustomer = await projects.CountAsync(
            project =>
                project.Status == ProjectStatus.NEED_BASIC_INFORMATION ||
                project.Status == ProjectStatus.QUOTATION_SENT,
            cancellationToken);

        var activeProjects = await projects.CountAsync(
            project =>
                project.Status != ProjectStatus.COMPLETED &&
                project.Status != ProjectStatus.REJECTED,
            cancellationToken);

        var overdueTasks = await projects.CountAsync(
            project =>
                project.TargetCompletionDate.HasValue &&
                project.TargetCompletionDate.Value < today,
            cancellationToken);

        var projectIds = projects.Select(project => project.ProjectId);
        var paymentFollowUp = await _db.OrderSet.CountAsync(
            order =>
                projectIds.Contains(order.ProjectId) &&
                order.Status != null &&
                (order.Status == OrderStatus.DEPOSIT_PENDING ||
                 (order.Status == OrderStatus.FINAL_PAYMENT_PENDING &&
                  (order.RemainingAmount ?? 0m) > 0m)),
            cancellationToken);

        var waitingConfirm = await _db.OrderSet.CountAsync(
            order =>
                projectIds.Contains(order.ProjectId) &&
                order.Status == OrderStatus.FINAL_PAYMENT_PENDING &&
                (order.RemainingAmount ?? 0m) <= 0m &&
                order.CustomerConfirmedDeliveryAt == null,
            cancellationToken);

        return new SalesDashboardKpisReadModel
        {
            NewRequests = newRequests,
            WaitingCustomer = waitingCustomer + waitingConfirm,
            PaymentFollowUp = paymentFollowUp,
            OverdueTasks = overdueTasks,
            ActiveProjects = activeProjects
        };
    }

    public async Task<IReadOnlyList<DashboardProjectQueueRowReadModel>> GetDesignerQueueRowsAsync(
        DashboardQueueFilterReadModel filter,
        CancellationToken cancellationToken = default)
    {
        var query = BuildDesignerProjectQuery(filter);
        return await ProjectToQueueRows(query).ToListAsync(cancellationToken);
    }

    public async Task<DesignerDashboardKpisReadModel> GetDesignerKpisAsync(
        DashboardQueueFilterReadModel filter,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(filter.UtcNow);
        var projects = BuildDesignerProjectQuery(filter);

        return new DesignerDashboardKpisReadModel
        {
            MeasurementDue = await projects.CountAsync(
                project => project.Status == ProjectStatus.MEASUREMENT_REQUIRED,
                cancellationToken),
            ProposalsInProgress = await projects.CountAsync(
                project =>
                    project.Status == ProjectStatus.SPACE_VERIFIED ||
                    project.Status == ProjectStatus.PROPOSAL_CONSULTING,
                cancellationToken),
            RevisionRequested = await projects.CountAsync(
                project => project.Status == ProjectStatus.QUOTATION_REVISION_REQUESTED,
                cancellationToken),
            OverdueTasks = await projects.CountAsync(
                project =>
                    project.TargetCompletionDate.HasValue &&
                    project.TargetCompletionDate.Value < today,
                cancellationToken)
        };
    }

    public async Task<IReadOnlyList<DashboardProductionQueueRowReadModel>> GetProductionQueueRowsAsync(
        DashboardQueueFilterReadModel filter,
        CancellationToken cancellationToken = default)
    {
        var query = BuildProductionQuery(filter, applyDateRange: true);
        return await ProjectProductionRows(query).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DashboardProductionCustomizationQueueRowReadModel>> GetProductionCustomizationQueueRowsAsync(
        DashboardQueueFilterReadModel filter,
        CancellationToken cancellationToken = default)
    {
        if (!IsScopeAll(filter.Scope))
        {
            return Array.Empty<DashboardProductionCustomizationQueueRowReadModel>();
        }

        return await BuildCustomizationQueueQuery(filter).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DashboardProductionDeliveryQueueRowReadModel>> GetProductionDeliveryQueueRowsAsync(
        DashboardQueueFilterReadModel filter,
        CancellationToken cancellationToken = default)
    {
        return await BuildDeliveryQueueQuery(filter).ToListAsync(cancellationToken);
    }

    public async Task<ProductionDashboardKpisReadModel> GetProductionKpisAsync(
        DashboardQueueFilterReadModel filter,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(filter.UtcNow);
        var activeRequests = BuildProductionQuery(filter, applyDateRange: false);

        var pendingStart = await activeRequests.CountAsync(
            request => request.Status == ProductionRequestStatus.PENDING,
            cancellationToken);

        var inProduction = await activeRequests.CountAsync(
            request => request.Status == ProductionRequestStatus.IN_PRODUCTION,
            cancellationToken);

        var readyToComplete = await activeRequests.CountAsync(
            request =>
                request.Status == ProductionRequestStatus.IN_PRODUCTION &&
                _db.ProductionItemSet.Any(item =>
                    item.ProductionRequestId == request.ProductionRequestId) &&
                !_db.ProductionItemSet.Any(item =>
                    item.ProductionRequestId == request.ProductionRequestId &&
                    (item.Status == null ||
                     !TerminalProductionItemStatuses.Contains(item.Status.Value))),
            cancellationToken);

        var overdueTasks = await activeRequests.CountAsync(
            request =>
                _db.ProjectPhaseTimelineSet.Any(timeline =>
                    timeline.ProjectId == request.ProjectId &&
                    timeline.Phase == ProjectPhaseType.PRODUCTION &&
                    timeline.DueDate < today &&
                    timeline.CompletedAt == null),
            cancellationToken);

        var deliveryOrders = BuildDeliveryOrderQuery(filter);
        var readyForDelivery = await deliveryOrders.CountAsync(cancellationToken);
        var awaitingDeliverySchedule = await deliveryOrders.CountAsync(
            order =>
                order.Status == OrderStatus.READY_FOR_DELIVERY &&
                !_db.ProjectScheduleSet.Any(schedule =>
                    schedule.ProjectId == order.ProjectId &&
                    schedule.ScheduleType == ProjectScheduleType.DELIVERY &&
                    schedule.Status != null &&
                    schedule.Status != ProjectScheduleStatus.CANCELLED),
            cancellationToken);

        var pendingCustomizationReview = 0;
        if (IsScopeAll(filter.Scope))
        {
            pendingCustomizationReview = await _db.CustomizationRequestVersionSet.CountAsync(
                version =>
                    version.Status == CustomizationVersionStatus.REVIEWING &&
                    version.FeasibilityStatus == ProductionFeasibilityStatus.PENDING &&
                    version.SubmittedForReviewAt != null,
                cancellationToken);
        }

        var completedQuery = BuildCompletedProductionQuery(filter);
        var completedInRange = await completedQuery.CountAsync(cancellationToken);

        return new ProductionDashboardKpisReadModel
        {
            PendingCustomizationReview = pendingCustomizationReview,
            PendingStart = pendingStart,
            PendingReview = pendingStart,
            InProduction = inProduction,
            ReadyToComplete = readyToComplete,
            OverdueTasks = overdueTasks,
            ReadyForDelivery = readyForDelivery,
            AwaitingDeliverySchedule = awaitingDeliverySchedule,
            CompletedInRange = completedInRange
        };
    }

    public Task<List<ProjectPhaseDeadlineRiskRowReadModel>> GetProjectPhaseDeadlineRiskRowsAsync(
        ProjectPhaseDeadlineRiskQueryReadModel query,
        CancellationToken cancellationToken = default)
    {
        var timelines = _db.ProjectPhaseTimelineSet.AsQueryable();

        if (query.Phase.HasValue)
        {
            timelines = timelines.Where(timeline => timeline.Phase == query.Phase.Value);
        }

        if (query.From.HasValue)
        {
            timelines = timelines.Where(timeline => timeline.DueDate >= query.From.Value);
        }

        if (query.To.HasValue)
        {
            timelines = timelines.Where(timeline => timeline.DueDate <= query.To.Value);
        }

        return PhaseDeadlineRiskRows(timelines, query).ToListAsync(cancellationToken);
    }

    private IQueryable<Domain.Entities.Project> BuildSalesProjectQuery(DashboardQueueFilterReadModel filter)
    {
        var projects = _db.ProjectSet.AsQueryable();
        projects = ApplyProjectScope(
            projects,
            filter,
            salesScoped: true,
            designerScoped: false);
        projects = ApplyProjectSearch(projects, filter.Search);
        projects = ApplyProjectDateRange(projects, filter.DateRange, filter.UtcNow);
        return projects;
    }

    private IQueryable<Domain.Entities.Project> BuildDesignerProjectQuery(DashboardQueueFilterReadModel filter)
    {
        var projects = _db.ProjectSet
            .Where(project =>
                project.Status.HasValue &&
                DesignerQueueStatuses.Contains(project.Status.Value));
        projects = ApplyProjectScope(
            projects,
            filter,
            salesScoped: false,
            designerScoped: true);
        projects = ApplyProjectSearch(projects, filter.Search);
        projects = ApplyProjectDateRange(projects, filter.DateRange, filter.UtcNow);
        return projects;
    }

    private IQueryable<Domain.Entities.ProductionRequest> BuildProductionQuery(
        DashboardQueueFilterReadModel filter,
        bool applyDateRange)
    {
        var requests = _db.ProductionRequestSet
            .Where(request =>
                request.Status.HasValue &&
                ActiveProductionStatuses.Contains(request.Status.Value));

        requests = ApplyProductionScope(requests, filter);
        requests = ApplyProductionSearch(requests, filter.Search);
        if (applyDateRange)
        {
            requests = ApplyProductionDateRange(requests, filter.DateRange, filter.UtcNow);
        }

        return requests;
    }

    private IQueryable<Domain.Entities.ProductionRequest> BuildCompletedProductionQuery(
        DashboardQueueFilterReadModel filter)
    {
        var requests = _db.ProductionRequestSet
            .Where(request => request.Status == ProductionRequestStatus.COMPLETED);

        requests = ApplyProductionScope(requests, filter);
        requests = ApplyProductionSearch(requests, filter.Search);

        if (string.IsNullOrWhiteSpace(filter.DateRange))
        {
            return requests.Where(request => request.ActualCompletionDate != null);
        }

        var today = DateOnly.FromDateTime(filter.UtcNow);
        var range = filter.DateRange.Trim();
        if (string.Equals(range, "today", StringComparison.OrdinalIgnoreCase))
        {
            return requests.Where(request => request.ActualCompletionDate == today);
        }

        if (string.Equals(range, "thisWeek", StringComparison.OrdinalIgnoreCase))
        {
            var start = today.AddDays(-(int)today.DayOfWeek);
            var end = start.AddDays(6);
            return requests.Where(request =>
                request.ActualCompletionDate >= start &&
                request.ActualCompletionDate <= end);
        }

        if (string.Equals(range, "thisMonth", StringComparison.OrdinalIgnoreCase))
        {
            return requests.Where(request =>
                request.ActualCompletionDate.HasValue &&
                request.ActualCompletionDate.Value.Year == today.Year &&
                request.ActualCompletionDate.Value.Month == today.Month);
        }

        return requests.Where(request => request.ActualCompletionDate != null);
    }

    private IQueryable<Domain.Entities.Order> BuildDeliveryOrderQuery(DashboardQueueFilterReadModel filter)
    {
        var orders = _db.OrderSet
            .Where(order =>
                order.Status != null &&
                DeliveryActiveOrderStatuses.Contains(order.Status.Value));

        var scope = NormalizeScope(filter.Scope);
        if (string.Equals(scope, "mine", StringComparison.OrdinalIgnoreCase))
        {
            orders = orders.Where(order =>
                _db.ProductionRequestSet.Any(request =>
                    request.OrderId == order.OrderId &&
                    request.AssignedTo == filter.CurrentUserId));
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = $"%{filter.Search.Trim()}%";
            orders = orders.Where(order =>
                _db.ProjectSet.Any(project =>
                    project.ProjectId == order.ProjectId &&
                    (EF.Functions.ILike(project.ProjectName, pattern) ||
                     (project.ProjectCode != null && EF.Functions.ILike(project.ProjectCode, pattern)) ||
                     _db.AccountSet.Any(account =>
                         account.AccountId == project.CustomerId &&
                         EF.Functions.ILike(account.FullName, pattern)))));
        }

        return orders;
    }

    private IQueryable<DashboardProductionCustomizationQueueRowReadModel> BuildCustomizationQueueQuery(
        DashboardQueueFilterReadModel filter)
    {
        var query =
            from version in _db.CustomizationRequestVersionSet
            join request in _db.CustomizationRequestSet
                on version.CustomizationRequestId equals request.CustomizationRequestId
            join project in _db.ProjectSet on request.ProjectId equals project.ProjectId
            where version.Status == CustomizationVersionStatus.REVIEWING &&
                  version.FeasibilityStatus == ProductionFeasibilityStatus.PENDING &&
                  version.SubmittedForReviewAt != null
            select new DashboardProductionCustomizationQueueRowReadModel
            {
                VersionId = version.CustomizationRequestVersionId,
                CustomizationRequestId = version.CustomizationRequestId,
                ProjectId = project.ProjectId,
                ProjectCode = project.ProjectCode,
                ProjectName = project.ProjectName,
                CustomerName = _db.AccountSet
                    .Where(account => account.AccountId == project.CustomerId)
                    .Select(account => account.FullName)
                    .FirstOrDefault() ?? string.Empty,
                VersionTitle = version.VersionTitle,
                MaterialAvailable = version.MaterialAvailable,
                SubmittedForReviewAt = version.SubmittedForReviewAt,
                UpdatedAt = version.UpdatedAt
            };

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = $"%{filter.Search.Trim()}%";
            query = query.Where(row =>
                EF.Functions.ILike(row.ProjectName, pattern) ||
                (row.ProjectCode != null && EF.Functions.ILike(row.ProjectCode, pattern)) ||
                EF.Functions.ILike(row.CustomerName, pattern));
        }

        return query
            .OrderByDescending(row => row.SubmittedForReviewAt ?? row.UpdatedAt)
            .ThenByDescending(row => row.VersionId);
    }

    private IQueryable<DashboardProductionDeliveryQueueRowReadModel> BuildDeliveryQueueQuery(
        DashboardQueueFilterReadModel filter)
    {
        var orders = BuildDeliveryOrderQuery(filter);

        return orders
            .Select(order => new DashboardProductionDeliveryQueueRowReadModel
            {
                OrderId = order.OrderId,
                ProjectId = order.ProjectId,
                ProjectCode = _db.ProjectSet
                    .Where(project => project.ProjectId == order.ProjectId)
                    .Select(project => project.ProjectCode)
                    .FirstOrDefault(),
                ProjectName = _db.ProjectSet
                    .Where(project => project.ProjectId == order.ProjectId)
                    .Select(project => project.ProjectName)
                    .FirstOrDefault() ?? string.Empty,
                CustomerName = _db.ProjectSet
                    .Where(project => project.ProjectId == order.ProjectId)
                    .Join(
                        _db.AccountSet,
                        project => project.CustomerId,
                        account => account.AccountId,
                        (_, account) => account.FullName)
                    .FirstOrDefault() ?? string.Empty,
                ProductionRequestId = _db.ProductionRequestSet
                    .Where(request => request.OrderId == order.OrderId)
                    .OrderByDescending(request => request.UpdatedAt ?? request.CreatedAt)
                    .ThenByDescending(request => request.ProductionRequestId)
                    .Select(request => (Guid?)request.ProductionRequestId)
                    .FirstOrDefault(),
                AssignedTo = _db.ProductionRequestSet
                    .Where(request => request.OrderId == order.OrderId && request.AssignedTo.HasValue)
                    .OrderByDescending(request => request.UpdatedAt ?? request.CreatedAt)
                    .ThenByDescending(request => request.ProductionRequestId)
                    .Select(request => request.AssignedTo)
                    .FirstOrDefault(),
                AssignedToName = _db.ProductionRequestSet
                    .Where(request => request.OrderId == order.OrderId && request.AssignedTo.HasValue)
                    .OrderByDescending(request => request.UpdatedAt ?? request.CreatedAt)
                    .ThenByDescending(request => request.ProductionRequestId)
                    .Join(
                        _db.AccountSet,
                        request => request.AssignedTo,
                        account => account.AccountId,
                        (_, account) => account.FullName)
                    .FirstOrDefault(),
                OrderStatus = order.Status ?? OrderStatus.READY_FOR_DELIVERY,
                DeliveryQueueStatus =
                    order.Status == OrderStatus.AWAITING_CUSTOMER_CONFIRMATION
                        ? "AWAITING_CUSTOMER_CONFIRMATION"
                        : _db.DeliverySet.Any(delivery =>
                              delivery.OrderId == order.OrderId &&
                              delivery.Status == DeliveryStatus.IN_PROGRESS)
                            ? "IN_PROGRESS"
                            : _db.ProjectScheduleSet.Any(schedule =>
                                  schedule.ProjectId == order.ProjectId &&
                                  schedule.ScheduleType == ProjectScheduleType.DELIVERY &&
                                  schedule.Status == ProjectScheduleStatus.CONFIRMED)
                                ? "SCHEDULED"
                                : "AWAITING_SCHEDULE",
                ScheduledEnd = _db.ProjectScheduleSet
                    .Where(schedule =>
                        schedule.ProjectId == order.ProjectId &&
                        schedule.ScheduleType == ProjectScheduleType.DELIVERY &&
                        schedule.Status != null &&
                        schedule.Status != ProjectScheduleStatus.CANCELLED)
                    .OrderByDescending(schedule => schedule.ScheduledEnd ?? schedule.ScheduledStart)
                    .Select(schedule => schedule.ScheduledEnd)
                    .FirstOrDefault(),
                UpdatedAt = order.UpdatedAt,
                CreatedAt = order.CreatedAt
            })
            .OrderByDescending(row => row.UpdatedAt ?? row.CreatedAt)
            .ThenByDescending(row => row.OrderId);
    }

    private static IQueryable<Domain.Entities.ProductionRequest> ApplyProductionScope(
        IQueryable<Domain.Entities.ProductionRequest> requests,
        DashboardQueueFilterReadModel filter)
    {
        var scope = NormalizeScope(filter.Scope);
        if (string.Equals(scope, "mine", StringComparison.OrdinalIgnoreCase))
        {
            return requests.Where(request => request.AssignedTo == filter.CurrentUserId);
        }

        return requests;
    }

    private IQueryable<Domain.Entities.ProductionRequest> ApplyProductionSearch(
        IQueryable<Domain.Entities.ProductionRequest> requests,
        string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return requests;
        }

        var pattern = $"%{search.Trim()}%";
        return requests.Where(request =>
            _db.ProjectSet.Any(project =>
                project.ProjectId == request.ProjectId &&
                (EF.Functions.ILike(project.ProjectName, pattern) ||
                 (project.ProjectCode != null && EF.Functions.ILike(project.ProjectCode, pattern)) ||
                 _db.AccountSet.Any(account =>
                     account.AccountId == project.CustomerId &&
                     EF.Functions.ILike(account.FullName, pattern)))));
    }

    private IQueryable<Domain.Entities.ProductionRequest> ApplyProductionDateRange(
        IQueryable<Domain.Entities.ProductionRequest> requests,
        string? dateRange,
        DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(dateRange))
        {
            return requests;
        }

        var today = DateOnly.FromDateTime(utcNow);
        var range = dateRange.Trim();
        if (string.Equals(range, "today", StringComparison.OrdinalIgnoreCase))
        {
            return requests.Where(request =>
                !_db.ProjectPhaseTimelineSet.Any(timeline =>
                    timeline.ProjectId == request.ProjectId &&
                    timeline.Phase == ProjectPhaseType.PRODUCTION) ||
                _db.ProjectPhaseTimelineSet.Any(timeline =>
                    timeline.ProjectId == request.ProjectId &&
                    timeline.Phase == ProjectPhaseType.PRODUCTION &&
                    timeline.DueDate == today));
        }

        if (string.Equals(range, "thisWeek", StringComparison.OrdinalIgnoreCase))
        {
            var start = today.AddDays(-(int)today.DayOfWeek);
            var end = start.AddDays(6);
            return requests.Where(request =>
                !_db.ProjectPhaseTimelineSet.Any(timeline =>
                    timeline.ProjectId == request.ProjectId &&
                    timeline.Phase == ProjectPhaseType.PRODUCTION) ||
                _db.ProjectPhaseTimelineSet.Any(timeline =>
                    timeline.ProjectId == request.ProjectId &&
                    timeline.Phase == ProjectPhaseType.PRODUCTION &&
                    timeline.DueDate >= start &&
                    timeline.DueDate <= end));
        }

        if (string.Equals(range, "thisMonth", StringComparison.OrdinalIgnoreCase))
        {
            return requests.Where(request =>
                !_db.ProjectPhaseTimelineSet.Any(timeline =>
                    timeline.ProjectId == request.ProjectId &&
                    timeline.Phase == ProjectPhaseType.PRODUCTION) ||
                _db.ProjectPhaseTimelineSet.Any(timeline =>
                    timeline.ProjectId == request.ProjectId &&
                    timeline.Phase == ProjectPhaseType.PRODUCTION &&
                    timeline.DueDate.Year == today.Year &&
                    timeline.DueDate.Month == today.Month));
        }

        return requests;
    }

    private IQueryable<DashboardProjectQueueRowReadModel> ProjectToQueueRows(
        IQueryable<Domain.Entities.Project> projects)
    {
        return projects
            .OrderByDescending(project => project.UpdatedAt ?? project.SubmittedAt ?? project.CreatedAt)
            .ThenByDescending(project => project.ProjectId)
            .Select(project => new DashboardProjectQueueRowReadModel
            {
                ProjectId = project.ProjectId,
                ProjectCode = project.ProjectCode,
                ProjectName = project.ProjectName,
                Status = project.Status,
                CustomerName = _db.AccountSet
                    .Where(account => account.AccountId == project.CustomerId)
                    .Select(account => account.FullName)
                    .FirstOrDefault() ?? string.Empty,
                AssignedSalesId = project.AssignedSalesId,
                AssignedSalesName = project.AssignedSalesId.HasValue
                    ? _db.AccountSet
                        .Where(account => account.AccountId == project.AssignedSalesId)
                        .Select(account => account.FullName)
                        .FirstOrDefault()
                    : null,
                AssignedDesignerId = project.AssignedDesignerId,
                AssignedDesignerName = project.AssignedDesignerId.HasValue
                    ? _db.AccountSet
                        .Where(account => account.AccountId == project.AssignedDesignerId)
                        .Select(account => account.FullName)
                        .FirstOrDefault()
                    : null,
                TargetCompletionDate = project.TargetCompletionDate,
                UpdatedAt = project.UpdatedAt,
                SubmittedAt = project.SubmittedAt,
                CreatedAt = project.CreatedAt,
                OrderId = _db.OrderSet
                    .Where(order =>
                        order.ProjectId == project.ProjectId &&
                        order.Status != null &&
                        NonCancelledOrderStatuses.Contains(order.Status.Value))
                    .OrderByDescending(order => order.UpdatedAt ?? order.CreatedAt)
                    .ThenByDescending(order => order.OrderId)
                    .Select(order => (Guid?)order.OrderId)
                    .FirstOrDefault(),
                OrderStatus = _db.OrderSet
                    .Where(order =>
                        order.ProjectId == project.ProjectId &&
                        order.Status != null &&
                        NonCancelledOrderStatuses.Contains(order.Status.Value))
                    .OrderByDescending(order => order.UpdatedAt ?? order.CreatedAt)
                    .ThenByDescending(order => order.OrderId)
                    .Select(order => order.Status)
                    .FirstOrDefault(),
                RemainingAmount = _db.OrderSet
                    .Where(order =>
                        order.ProjectId == project.ProjectId &&
                        order.Status != null &&
                        NonCancelledOrderStatuses.Contains(order.Status.Value))
                    .OrderByDescending(order => order.UpdatedAt ?? order.CreatedAt)
                    .ThenByDescending(order => order.OrderId)
                    .Select(order => order.RemainingAmount)
                    .FirstOrDefault(),
                CustomerConfirmedDeliveryAt = _db.OrderSet
                    .Where(order =>
                        order.ProjectId == project.ProjectId &&
                        order.Status != null &&
                        NonCancelledOrderStatuses.Contains(order.Status.Value))
                    .OrderByDescending(order => order.UpdatedAt ?? order.CreatedAt)
                    .ThenByDescending(order => order.OrderId)
                    .Select(order => order.CustomerConfirmedDeliveryAt)
                    .FirstOrDefault(),
                OrderUpdatedAt = _db.OrderSet
                    .Where(order =>
                        order.ProjectId == project.ProjectId &&
                        order.Status != null &&
                        NonCancelledOrderStatuses.Contains(order.Status.Value))
                    .OrderByDescending(order => order.UpdatedAt ?? order.CreatedAt)
                    .ThenByDescending(order => order.OrderId)
                    .Select(order => order.UpdatedAt)
                    .FirstOrDefault()
            });
    }

    private IQueryable<DashboardProductionQueueRowReadModel> ProjectProductionRows(
        IQueryable<Domain.Entities.ProductionRequest> requests)
    {
        return requests
            .OrderByDescending(request => request.UpdatedAt ?? request.CreatedAt)
            .ThenByDescending(request => request.ProductionRequestId)
            .Select(request => new DashboardProductionQueueRowReadModel
            {
                ProductionRequestId = request.ProductionRequestId,
                ProductionCode = request.ProductionCode,
                ProjectId = request.ProjectId,
                OrderId = request.OrderId,
                ProjectCode = _db.ProjectSet
                    .Where(project => project.ProjectId == request.ProjectId)
                    .Select(project => project.ProjectCode)
                    .FirstOrDefault(),
                ProjectName = _db.ProjectSet
                    .Where(project => project.ProjectId == request.ProjectId)
                    .Select(project => project.ProjectName)
                    .FirstOrDefault() ?? string.Empty,
                CustomerName = _db.ProjectSet
                    .Where(project => project.ProjectId == request.ProjectId)
                    .Join(
                        _db.AccountSet,
                        project => project.CustomerId,
                        account => account.AccountId,
                        (_, account) => account.FullName)
                    .FirstOrDefault() ?? string.Empty,
                AssignedTo = request.AssignedTo,
                AssignedToName = request.AssignedTo.HasValue
                    ? _db.AccountSet
                        .Where(account => account.AccountId == request.AssignedTo)
                        .Select(account => account.FullName)
                        .FirstOrDefault()
                    : null,
                Status = request.Status ?? ProductionRequestStatus.PENDING,
                Priority = request.Priority,
                ProductionDeadline = _db.ProjectPhaseTimelineSet
                    .Where(timeline =>
                        timeline.ProjectId == request.ProjectId &&
                        timeline.Phase == ProjectPhaseType.PRODUCTION)
                    .Select(timeline => (DateOnly?)timeline.DueDate)
                    .FirstOrDefault(),
                BlockedItemCount = _db.ProductionItemSet.Count(item =>
                    item.ProductionRequestId == request.ProductionRequestId &&
                    item.Status == ProductionItemStatus.CANCELLED),
                AllItemsTerminal = _db.ProductionItemSet.Any(item =>
                        item.ProductionRequestId == request.ProductionRequestId) &&
                    !_db.ProductionItemSet.Any(item =>
                        item.ProductionRequestId == request.ProductionRequestId &&
                        (item.Status == null ||
                         !TerminalProductionItemStatuses.Contains(item.Status.Value))),
                UpdatedAt = request.UpdatedAt,
                CreatedAt = request.CreatedAt
            });
    }

    private IQueryable<ProjectPhaseDeadlineRiskRowReadModel> PhaseDeadlineRiskRows(
        IQueryable<Domain.Entities.ProjectPhaseTimeline> timelines,
        ProjectPhaseDeadlineRiskQueryReadModel query)
    {
        var rows =
            from timeline in timelines
            join project in _db.ProjectSet on timeline.ProjectId equals project.ProjectId
            where (!query.SalesId.HasValue || project.AssignedSalesId == query.SalesId.Value) &&
                  (!query.DesignerId.HasValue || project.AssignedDesignerId == query.DesignerId.Value) &&
                  (!query.ProductionId.HasValue ||
                   _db.ProductionRequestSet.Any(request =>
                       request.ProjectId == project.ProjectId &&
                       request.AssignedTo == query.ProductionId.Value))
            orderby timeline.DueDate, timeline.Phase, project.ProjectCode
            select new ProjectPhaseDeadlineRiskRowReadModel
            {
                ProjectId = project.ProjectId,
                ProjectCode = project.ProjectCode,
                ProjectName = project.ProjectName,
                Phase = timeline.Phase,
                DueDate = timeline.DueDate,
                CompletedAt = timeline.CompletedAt,
                ProjectStatus = project.Status,
                AssignedSalesId = project.AssignedSalesId,
                AssignedSalesName = project.AssignedSalesId.HasValue
                    ? _db.AccountSet
                        .Where(account => account.AccountId == project.AssignedSalesId)
                        .Select(account => account.FullName)
                        .FirstOrDefault()
                    : null,
                AssignedDesignerId = project.AssignedDesignerId,
                AssignedDesignerName = project.AssignedDesignerId.HasValue
                    ? _db.AccountSet
                        .Where(account => account.AccountId == project.AssignedDesignerId)
                        .Select(account => account.FullName)
                        .FirstOrDefault()
                    : null,
                AssignedProductionId = _db.ProductionRequestSet
                    .Where(request => request.ProjectId == project.ProjectId && request.AssignedTo.HasValue)
                    .OrderByDescending(request => request.UpdatedAt ?? request.CreatedAt)
                    .ThenByDescending(request => request.ProductionRequestId)
                    .Select(request => request.AssignedTo)
                    .FirstOrDefault(),
                AssignedProductionName = _db.ProductionRequestSet
                    .Where(request => request.ProjectId == project.ProjectId && request.AssignedTo.HasValue)
                    .OrderByDescending(request => request.UpdatedAt ?? request.CreatedAt)
                    .ThenByDescending(request => request.ProductionRequestId)
                    .Join(
                        _db.AccountSet,
                        request => request.AssignedTo,
                        account => account.AccountId,
                        (_, account) => account.FullName)
                    .FirstOrDefault()
            };

        return rows;
    }

    private static IQueryable<Domain.Entities.Project> ApplyProjectScope(
        IQueryable<Domain.Entities.Project> projects,
        DashboardQueueFilterReadModel filter,
        bool salesScoped,
        bool designerScoped)
    {
        var scope = NormalizeScope(filter.Scope);
        var isAdmin = IsAdmin(filter.CurrentUserRole);

        if (string.Equals(scope, "mine", StringComparison.OrdinalIgnoreCase))
        {
            if (salesScoped)
            {
                return projects.Where(project => project.AssignedSalesId == filter.CurrentUserId);
            }

            if (designerScoped)
            {
                return projects.Where(project => project.AssignedDesignerId == filter.CurrentUserId);
            }
        }

        if (isAdmin && string.Equals(scope, "all", StringComparison.OrdinalIgnoreCase))
        {
            return projects;
        }

        // team (and non-admin all): projects with an assignee in this role pool
        if (salesScoped)
        {
            return projects.Where(project => project.AssignedSalesId != null);
        }

        if (designerScoped)
        {
            return projects.Where(project => project.AssignedDesignerId != null);
        }

        return projects;
    }

    private IQueryable<Domain.Entities.Project> ApplyProjectSearch(
        IQueryable<Domain.Entities.Project> projects,
        string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return projects;
        }

        var pattern = $"%{search.Trim()}%";
        return projects.Where(project =>
            EF.Functions.ILike(project.ProjectName, pattern) ||
            (project.ProjectCode != null && EF.Functions.ILike(project.ProjectCode, pattern)) ||
            _db.AccountSet.Any(account =>
                account.AccountId == project.CustomerId &&
                EF.Functions.ILike(account.FullName, pattern)));
    }

    private static IQueryable<Domain.Entities.Project> ApplyProjectDateRange(
        IQueryable<Domain.Entities.Project> projects,
        string? dateRange,
        DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(dateRange))
        {
            return projects;
        }

        var today = DateOnly.FromDateTime(utcNow);
        var range = dateRange.Trim();
        if (string.Equals(range, "today", StringComparison.OrdinalIgnoreCase))
        {
            return projects.Where(project =>
                !project.TargetCompletionDate.HasValue ||
                project.TargetCompletionDate == today);
        }

        if (string.Equals(range, "thisWeek", StringComparison.OrdinalIgnoreCase))
        {
            var start = today.AddDays(-(int)today.DayOfWeek);
            var end = start.AddDays(6);
            return projects.Where(project =>
                !project.TargetCompletionDate.HasValue ||
                (project.TargetCompletionDate >= start && project.TargetCompletionDate <= end));
        }

        if (string.Equals(range, "thisMonth", StringComparison.OrdinalIgnoreCase))
        {
            return projects.Where(project =>
                !project.TargetCompletionDate.HasValue ||
                (project.TargetCompletionDate.Value.Year == today.Year &&
                 project.TargetCompletionDate.Value.Month == today.Month));
        }

        return projects;
    }

    private static string NormalizeScope(string? scope)
    {
        return string.IsNullOrWhiteSpace(scope) ? "mine" : scope.Trim();
    }

    private static bool IsScopeAll(string? scope)
    {
        return string.Equals(NormalizeScope(scope), "all", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAdmin(string? roleName)
    {
        return string.Equals(roleName, "ADMIN", StringComparison.OrdinalIgnoreCase);
    }
}
