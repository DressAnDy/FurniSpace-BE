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
        var query = BuildProductionQuery(filter);
        return await ProjectProductionRows(query).ToListAsync(cancellationToken);
    }

    public async Task<ProductionDashboardKpisReadModel> GetProductionKpisAsync(
        DashboardQueueFilterReadModel filter,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(filter.UtcNow);
        var requests = BuildProductionQuery(filter);

        return new ProductionDashboardKpisReadModel
        {
            PendingReview = await requests.CountAsync(
                request => request.Status == ProductionRequestStatus.PENDING,
                cancellationToken),
            InProduction = await requests.CountAsync(
                request => request.Status == ProductionRequestStatus.IN_PRODUCTION,
                cancellationToken),
            ReadyToComplete = await requests.CountAsync(
                request => false,
                cancellationToken),
            OverdueTasks = await requests.CountAsync(
                request =>
                    _db.ProjectPhaseTimelineSet.Any(timeline =>
                        timeline.ProjectId == request.ProjectId &&
                        timeline.Phase == ProjectPhaseType.PRODUCTION &&
                        timeline.DueDate < today),
                cancellationToken)
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
        DashboardQueueFilterReadModel filter)
    {
        var requests = _db.ProductionRequestSet
            .Where(request =>
                request.Status.HasValue &&
                ActiveProductionStatuses.Contains(request.Status.Value));

        requests = ApplyProductionScope(requests, filter);
        requests = ApplyProductionSearch(requests, filter.Search);
        requests = ApplyProductionDateRange(requests, filter.DateRange, filter.UtcNow);
        return requests;
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
                  (!query.DesignerId.HasValue || project.AssignedDesignerId == query.DesignerId.Value)
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

    private static bool IsAdmin(string? roleName)
    {
        return string.Equals(roleName, "ADMIN", StringComparison.OrdinalIgnoreCase);
    }
}
