#nullable enable

using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Dashboard;
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

    /// <summary>
    /// Remaining payment is incurred only after customer delivery confirmation.
    /// Earlier collect stages (start fee, deposit, production, in-transit delivery) are not this KPI.
    /// </summary>
    private static readonly OrderStatus[] RemainingNotYetIncurredStatuses =
    [
        OrderStatus.CREATED,
        OrderStatus.DEPOSIT_PENDING,
        OrderStatus.DEPOSIT_PAID,
        OrderStatus.IN_PRODUCTION,
        OrderStatus.READY_FOR_DELIVERY,
        OrderStatus.DELIVERING,
        OrderStatus.AWAITING_CUSTOMER_CONFIRMATION,
        OrderStatus.CANCELLED
    ];

    private static readonly PaymentStatus[] OpenRemainingPaymentStatuses =
    [
        PaymentStatus.PENDING,
        PaymentStatus.PROCESSING,
        PaymentStatus.EXPIRED
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
        var stockProjects = BuildSalesStockProjectQuery(filter);
        var acceptedProjectsQuery = BuildAcceptedSalesProjectQuery(filter);

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

        var acceptedProjects = await acceptedProjectsQuery.CountAsync(cancellationToken);

        // Stock overdue: targetCompletionDate < today (UTC). No status is excluded.
        // dateRange and search are ignored so a period filter cannot hide the current backlog.
        var overdueTasks = await stockProjects.CountAsync(
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

        var unpaidRemaining = await BuildUnpaidRemainingOrdersQuery(acceptedProjectsQuery)
            .CountAsync(cancellationToken);

        return new SalesDashboardKpisReadModel
        {
            AcceptedProjects = acceptedProjects,
            UnpaidRemaining = unpaidRemaining,
            NewRequests = newRequests,
            WaitingCustomer = waitingCustomer + waitingConfirm,
            PaymentFollowUp = paymentFollowUp,
            OverdueTasks = overdueTasks,
            ActiveProjects = activeProjects
        };
    }

    public async Task<IReadOnlyList<SalesUnpaidRemainingRowReadModel>> GetSalesUnpaidRemainingRowsAsync(
        DashboardQueueFilterReadModel filter,
        CancellationToken cancellationToken = default)
    {
        var acceptedProjects = BuildAcceptedSalesProjectQuery(filter);
        var orders = BuildUnpaidRemainingOrdersQuery(acceptedProjects);

        return await orders
            .Select(order => new SalesUnpaidRemainingRowReadModel
            {
                OrderId = order.OrderId,
                OrderCode = order.OrderCode,
                ProjectId = order.ProjectId,
                ProjectCode = _db.ProjectSet
                    .Where(project => project.ProjectId == order.ProjectId)
                    .Select(project => project.ProjectCode)
                    .FirstOrDefault(),
                ProjectName = _db.ProjectSet
                    .Where(project => project.ProjectId == order.ProjectId)
                    .Select(project => project.ProjectName)
                    .FirstOrDefault() ?? string.Empty,
                CustomerId = order.CustomerId,
                CustomerName = _db.AccountSet
                    .Where(account => account.AccountId == order.CustomerId)
                    .Select(account => account.FullName)
                    .FirstOrDefault() ?? string.Empty,
                AssignedSalesId = _db.ProjectSet
                    .Where(project => project.ProjectId == order.ProjectId)
                    .Select(project => project.AssignedSalesId)
                    .FirstOrDefault(),
                AssignedSalesName = _db.ProjectSet
                    .Where(project => project.ProjectId == order.ProjectId && project.AssignedSalesId.HasValue)
                    .Join(
                        _db.AccountSet,
                        project => project.AssignedSalesId,
                        account => account.AccountId,
                        (_, account) => account.FullName)
                    .FirstOrDefault(),
                Status = order.Status ?? OrderStatus.FINAL_PAYMENT_PENDING,
                RemainingAmount = order.RemainingAmount ?? 0m,
                Currency = _db.PaymentSet
                    .Where(payment =>
                        payment.OrderId == order.OrderId &&
                        payment.PaymentType == PaymentType.REMAINING_PAYMENT)
                    .OrderByDescending(payment => payment.UpdatedAt ?? payment.CreatedAt)
                    .ThenByDescending(payment => payment.PaymentId)
                    .Select(payment => payment.Currency)
                    .FirstOrDefault() ?? "VND",
                PaymentId = _db.PaymentSet
                    .Where(payment =>
                        payment.OrderId == order.OrderId &&
                        payment.PaymentType == PaymentType.REMAINING_PAYMENT &&
                        payment.Status != null &&
                        OpenRemainingPaymentStatuses.Contains(payment.Status.Value))
                    .OrderByDescending(payment => payment.UpdatedAt ?? payment.CreatedAt)
                    .ThenByDescending(payment => payment.PaymentId)
                    .Select(payment => (Guid?)payment.PaymentId)
                    .FirstOrDefault(),
                PaymentStatus = _db.PaymentSet
                    .Where(payment =>
                        payment.OrderId == order.OrderId &&
                        payment.PaymentType == PaymentType.REMAINING_PAYMENT &&
                        payment.Status != null &&
                        OpenRemainingPaymentStatuses.Contains(payment.Status.Value))
                    .OrderByDescending(payment => payment.UpdatedAt ?? payment.CreatedAt)
                    .ThenByDescending(payment => payment.PaymentId)
                    .Select(payment => payment.Status)
                    .FirstOrDefault(),
                UpdatedAt = order.UpdatedAt ?? order.CreatedAt ?? filter.UtcNow
            })
            .OrderByDescending(row => row.UpdatedAt)
            .ThenByDescending(row => row.OrderId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SalesOverdueTaskRowReadModel>> GetSalesOverdueTaskRowsAsync(
        DashboardQueueFilterReadModel filter,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(filter.UtcNow);
        var projects = BuildSalesStockProjectQuery(filter)
            .Where(project =>
                project.TargetCompletionDate.HasValue &&
                project.TargetCompletionDate.Value < today);

        var rows = await projects
            .OrderBy(project => project.TargetCompletionDate)
            .ThenBy(project => project.ProjectId)
            .Select(project => new SalesOverdueTaskRowReadModel
            {
                ProjectId = project.ProjectId,
                ProjectCode = project.ProjectCode,
                ProjectName = project.ProjectName,
                CustomerId = project.CustomerId,
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
                Status = project.Status,
                TargetCompletionDate = project.TargetCompletionDate!.Value,
                OverdueDays = 0,
                SubmittedAt = project.SubmittedAt,
                UpdatedAt = project.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new SalesOverdueTaskRowReadModel
            {
                ProjectId = row.ProjectId,
                ProjectCode = row.ProjectCode,
                ProjectName = row.ProjectName,
                CustomerId = row.CustomerId,
                CustomerName = row.CustomerName,
                AssignedSalesId = row.AssignedSalesId,
                AssignedSalesName = row.AssignedSalesName,
                Status = row.Status,
                TargetCompletionDate = row.TargetCompletionDate,
                OverdueDays = today.DayNumber - row.TargetCompletionDate.DayNumber,
                SubmittedAt = row.SubmittedAt,
                UpdatedAt = row.UpdatedAt
            })
            .ToList();
    }

    private IQueryable<Domain.Entities.Order> BuildUnpaidRemainingOrdersQuery(
        IQueryable<Domain.Entities.Project> acceptedProjects)
    {
        var acceptedProjectIds = acceptedProjects.Select(project => project.ProjectId);
        return _db.OrderSet.Where(order =>
            acceptedProjectIds.Contains(order.ProjectId) &&
            order.Status != null &&
            !RemainingNotYetIncurredStatuses.Contains(order.Status.Value) &&
            (
                ((order.RemainingAmount ?? 0m) > 0m &&
                 (order.Status == OrderStatus.FINAL_PAYMENT_PENDING ||
                  order.CustomerConfirmedDeliveryAt != null))
                ||
                _db.PaymentSet.Any(payment =>
                    payment.OrderId == order.OrderId &&
                    payment.PaymentType == PaymentType.REMAINING_PAYMENT &&
                    payment.Status != null &&
                    OpenRemainingPaymentStatuses.Contains(payment.Status.Value))));
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
        var measurementDue = await BuildConfirmedMeasurementSchedulesQuery(filter)
            .CountAsync(cancellationToken);
        var proposalsInProgress = await BuildProposalConsultingProjectsQuery(filter)
            .CountAsync(cancellationToken);
        var revisionRequested = await BuildRevisionRequestedProposalsQuery(filter)
            .CountAsync(cancellationToken);

        return new DesignerDashboardKpisReadModel
        {
            MeasurementDue = measurementDue,
            ProposalsInProgress = proposalsInProgress,
            RevisionRequested = revisionRequested,
            OverdueTasks = await projects.CountAsync(
                project =>
                    project.TargetCompletionDate.HasValue &&
                    project.TargetCompletionDate.Value < today,
                cancellationToken)
        };
    }

    public async Task<IReadOnlyList<DesignerConfirmedMeasurementRowReadModel>> GetDesignerConfirmedMeasurementRowsAsync(
        DashboardQueueFilterReadModel filter,
        CancellationToken cancellationToken = default)
    {
        var schedules = BuildConfirmedMeasurementSchedulesQuery(filter);

        return await schedules
            .OrderBy(schedule => schedule.ScheduledStart)
            .ThenBy(schedule => schedule.ScheduleId)
            .Select(schedule => new DesignerConfirmedMeasurementRowReadModel
            {
                ScheduleId = schedule.ScheduleId,
                ProjectId = schedule.ProjectId,
                ProjectCode = _db.ProjectSet
                    .Where(project => project.ProjectId == schedule.ProjectId)
                    .Select(project => project.ProjectCode)
                    .FirstOrDefault(),
                ProjectName = _db.ProjectSet
                    .Where(project => project.ProjectId == schedule.ProjectId)
                    .Select(project => project.ProjectName)
                    .FirstOrDefault() ?? string.Empty,
                Title = schedule.Title,
                ScheduledStart = schedule.ScheduledStart,
                ScheduledEnd = schedule.ScheduledEnd,
                Location = schedule.Location,
                Status = schedule.Status ?? ProjectScheduleStatus.CONFIRMED,
                AssignedStaffId = schedule.AssignedStaffId,
                AssignedStaffName = schedule.AssignedStaffId.HasValue
                    ? _db.AccountSet
                        .Where(account => account.AccountId == schedule.AssignedStaffId)
                        .Select(account => account.FullName)
                        .FirstOrDefault()
                    : null
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DesignerProposalConsultingRowReadModel>> GetDesignerProposalConsultingRowsAsync(
        DashboardQueueFilterReadModel filter,
        CancellationToken cancellationToken = default)
    {
        var projects = BuildProposalConsultingProjectsQuery(filter);

        return await projects
            .OrderByDescending(project => project.UpdatedAt ?? project.CreatedAt)
            .ThenByDescending(project => project.ProjectId)
            .Select(project => new DesignerProposalConsultingRowReadModel
            {
                ProjectId = project.ProjectId,
                ProjectCode = project.ProjectCode,
                ProjectName = project.ProjectName,
                CustomerId = project.CustomerId,
                CustomerName = _db.AccountSet
                    .Where(account => account.AccountId == project.CustomerId)
                    .Select(account => account.FullName)
                    .FirstOrDefault() ?? string.Empty,
                AssignedDesignerId = project.AssignedDesignerId,
                AssignedDesignerName = project.AssignedDesignerId.HasValue
                    ? _db.AccountSet
                        .Where(account => account.AccountId == project.AssignedDesignerId)
                        .Select(account => account.FullName)
                        .FirstOrDefault()
                    : null,
                Status = project.Status ?? ProjectStatus.PROPOSAL_CONSULTING,
                DesignerAssignedAt = project.DesignerAssignedAt,
                UpdatedAt = project.UpdatedAt,
                SubmittedAt = project.SubmittedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DesignerRevisionRequestedRowReadModel>> GetDesignerRevisionRequestedRowsAsync(
        DashboardQueueFilterReadModel filter,
        CancellationToken cancellationToken = default)
    {
        var proposals = BuildRevisionRequestedProposalsQuery(filter);

        return await proposals
            .OrderByDescending(proposal => proposal.UpdatedAt ?? proposal.CreatedAt)
            .ThenByDescending(proposal => proposal.ProposalId)
            .Select(proposal => new DesignerRevisionRequestedRowReadModel
            {
                ProposalId = proposal.ProposalId,
                ProposalName = proposal.ProposalName,
                Status = proposal.Status ?? ProposalStatus.REVISION_REQUESTED,
                RevisionNote = proposal.RevisionNote,
                ProjectId = proposal.ProjectId,
                ProjectCode = _db.ProjectSet
                    .Where(project => project.ProjectId == proposal.ProjectId)
                    .Select(project => project.ProjectCode)
                    .FirstOrDefault(),
                ProjectName = _db.ProjectSet
                    .Where(project => project.ProjectId == proposal.ProjectId)
                    .Select(project => project.ProjectName)
                    .FirstOrDefault() ?? string.Empty,
                AssignedDesignerId = _db.ProjectSet
                    .Where(project => project.ProjectId == proposal.ProjectId)
                    .Select(project => project.AssignedDesignerId)
                    .FirstOrDefault(),
                AssignedDesignerName = _db.ProjectSet
                    .Where(project => project.ProjectId == proposal.ProjectId && project.AssignedDesignerId.HasValue)
                    .Join(
                        _db.AccountSet,
                        project => project.AssignedDesignerId,
                        account => account.AccountId,
                        (_, account) => account.FullName)
                    .FirstOrDefault(),
                RevisionRequestedAt = proposal.UpdatedAt ?? proposal.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    private IQueryable<Domain.Entities.ProjectSchedule> BuildConfirmedMeasurementSchedulesQuery(
        DashboardQueueFilterReadModel filter)
    {
        var schedules = _db.ProjectScheduleSet.Where(schedule =>
            schedule.ScheduleType == ProjectScheduleType.MEASUREMENT &&
            schedule.Status == ProjectScheduleStatus.CONFIRMED);

        schedules = ApplyScheduleAssigneeScope(schedules, filter);

        var bounds = DashboardScheduleDateRange.TryResolve(filter.DateRange, filter.UtcNow);
        if (bounds.HasValue)
        {
            var fromUtc = bounds.Value.FromUtc;
            var toUtcExclusive = bounds.Value.ToUtcExclusive;
            schedules = schedules.Where(schedule =>
                schedule.ScheduledStart >= fromUtc &&
                schedule.ScheduledStart < toUtcExclusive);
        }

        return schedules;
    }

    /// <summary>
    /// Projects currently in <c>PROPOSAL_CONSULTING</c>. Date filter uses
    /// <c>updatedAt</c> (fallback <c>createdAt</c>) in Asia/Ho_Chi_Minh dateRange —
    /// there is no dedicated "entered consulting" timestamp.
    /// </summary>
    private IQueryable<Domain.Entities.Project> BuildProposalConsultingProjectsQuery(
        DashboardQueueFilterReadModel filter)
    {
        var projects = _db.ProjectSet.Where(project =>
            project.Status == ProjectStatus.PROPOSAL_CONSULTING);

        projects = ApplyDesignerAssigneeScope(projects, filter);

        var bounds = DashboardScheduleDateRange.TryResolve(filter.DateRange, filter.UtcNow);
        if (bounds.HasValue)
        {
            var fromUtc = bounds.Value.FromUtc;
            var toUtcExclusive = bounds.Value.ToUtcExclusive;
            // Prefer updatedAt; fall back to createdAt when updatedAt is null.
            projects = projects.Where(project =>
                project.UpdatedAt.HasValue
                    ? project.UpdatedAt.Value >= fromUtc && project.UpdatedAt.Value < toUtcExclusive
                    : project.CreatedAt.HasValue &&
                      project.CreatedAt.Value >= fromUtc &&
                      project.CreatedAt.Value < toUtcExclusive);
        }

        return projects;
    }

    /// <summary>
    /// Proposals with status <c>REVISION_REQUESTED</c>, scoped via parent project designer.
    /// Date filter uses proposal <c>updatedAt</c> (fallback <c>createdAt</c>) — no
    /// <c>revisionRequestedAt</c> column yet.
    /// </summary>
    private IQueryable<Domain.Entities.Proposal> BuildRevisionRequestedProposalsQuery(
        DashboardQueueFilterReadModel filter)
    {
        var scopedProjectIds = ApplyDesignerAssigneeScope(_db.ProjectSet.AsQueryable(), filter)
            .Select(project => project.ProjectId);

        var proposals = _db.ProposalSet.Where(proposal =>
            proposal.Status == ProposalStatus.REVISION_REQUESTED &&
            scopedProjectIds.Contains(proposal.ProjectId));

        var bounds = DashboardScheduleDateRange.TryResolve(filter.DateRange, filter.UtcNow);
        if (bounds.HasValue)
        {
            var fromUtc = bounds.Value.FromUtc;
            var toUtcExclusive = bounds.Value.ToUtcExclusive;
            proposals = proposals.Where(proposal =>
                proposal.UpdatedAt.HasValue
                    ? proposal.UpdatedAt.Value >= fromUtc && proposal.UpdatedAt.Value < toUtcExclusive
                    : proposal.CreatedAt.HasValue &&
                      proposal.CreatedAt.Value >= fromUtc &&
                      proposal.CreatedAt.Value < toUtcExclusive);
        }

        return proposals;
    }

    private static IQueryable<Domain.Entities.Project> ApplyDesignerAssigneeScope(
        IQueryable<Domain.Entities.Project> projects,
        DashboardQueueFilterReadModel filter)
    {
        var scope = NormalizeScope(filter.Scope);
        var isAdmin = IsAdmin(filter.CurrentUserRole);

        if (string.Equals(scope, "mine", StringComparison.OrdinalIgnoreCase))
        {
            return projects.Where(project => project.AssignedDesignerId == filter.CurrentUserId);
        }

        if (isAdmin && string.Equals(scope, "all", StringComparison.OrdinalIgnoreCase))
        {
            return projects;
        }

        return projects.Where(project => project.AssignedDesignerId != null);
    }

    private static IQueryable<Domain.Entities.ProjectSchedule> ApplyScheduleAssigneeScope(
        IQueryable<Domain.Entities.ProjectSchedule> schedules,
        DashboardQueueFilterReadModel filter)
    {
        var scope = NormalizeScope(filter.Scope);
        var isAdmin = IsAdmin(filter.CurrentUserRole);

        if (string.Equals(scope, "mine", StringComparison.OrdinalIgnoreCase))
        {
            return schedules.Where(schedule => schedule.AssignedStaffId == filter.CurrentUserId);
        }

        if (isAdmin && string.Equals(scope, "all", StringComparison.OrdinalIgnoreCase))
        {
            return schedules;
        }

        // team (and non-admin all): must have an assignee
        return schedules.Where(schedule => schedule.AssignedStaffId != null);
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
        var projects = BuildSalesStockProjectQuery(filter);
        projects = ApplyProjectSearch(projects, filter.Search);
        projects = ApplyProjectDateRange(projects, filter.DateRange, filter.UtcNow);
        return projects;
    }

    private IQueryable<Domain.Entities.Project> BuildSalesStockProjectQuery(DashboardQueueFilterReadModel filter)
    {
        return ApplyProjectScope(
            _db.ProjectSet.AsQueryable(),
            filter,
            salesScoped: true,
            designerScoped: false);
    }

    /// <summary>
    /// Projects a sales user has accepted for consultation: <c>assignedSalesId</c> is set.
    /// Status is not filtered. Unassigned requests, including <c>SUBMITTED</c>, are excluded.
    /// </summary>
    private IQueryable<Domain.Entities.Project> BuildAcceptedSalesProjectQuery(DashboardQueueFilterReadModel filter)
    {
        var projects = _db.ProjectSet.Where(project => project.AssignedSalesId != null);
        if (string.Equals(NormalizeScope(filter.Scope), "mine", StringComparison.OrdinalIgnoreCase))
        {
            projects = projects.Where(project => project.AssignedSalesId == filter.CurrentUserId);
        }

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
