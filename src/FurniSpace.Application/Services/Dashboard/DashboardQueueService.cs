using FurniSpace.Application.Common;
using FurniSpace.Application.Common.Dashboard;
using FurniSpace.Application.Constants.Common;
using FurniSpace.Application.DTOs.Dashboard;
using FurniSpace.Application.Interfaces.Dashboard;
using FurniSpace.Infrastructure.ReadModels.Dashboard;
using FurniSpace.Infrastructure.Repositories.IRepository;
using static FurniSpace.Application.Constants.Dashboard.DashboardQueueConstants;

namespace FurniSpace.Application.Services.Dashboard;

public sealed class DashboardQueueService : IDashboardQueueService
{
    private readonly IDashboardQueueReadRepository _dashboard;
    private readonly IProjectRepository _projects;

    public DashboardQueueService(
        IDashboardQueueReadRepository dashboard,
        IProjectRepository projects)
    {
        _dashboard = dashboard;
        _projects = projects;
    }

    public async Task<ServiceResult<DashboardQueueResponseDto>> GetSalesActionQueueAsync(
        Guid currentUserId,
        DashboardQueueQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var prepared = await PrepareAsync(currentUserId, query, ApplicationRoles.Sales, cancellationToken);
        if (prepared.Error is not null)
        {
            return prepared.Error;
        }

        var rows = await _dashboard.GetSalesQueueRowsAsync(prepared.Filter!, cancellationToken);
        var items = rows
            .Select(row => MapSalesItem(row, prepared.UtcNow))
            .Where(item => MatchesGroupAndPriority(item, prepared.Query!))
            .ToList();

        return ServiceResult<DashboardQueueResponseDto>.Success(
            BuildQueueResponse(items, prepared.Query!),
            "Sales action queue retrieved successfully.");
    }

    public async Task<ServiceResult<SalesDashboardKpisDto>> GetSalesKpisAsync(
        Guid currentUserId,
        DashboardQueueQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var prepared = await PrepareAsync(currentUserId, query, ApplicationRoles.Sales, cancellationToken);
        if (prepared.Error is not null)
        {
            return MapError<SalesDashboardKpisDto>(prepared.Error);
        }

        var kpis = await _dashboard.GetSalesKpisAsync(prepared.Filter!, cancellationToken);
        return ServiceResult<SalesDashboardKpisDto>.Success(
            new SalesDashboardKpisDto
            {
                NewRequests = kpis.NewRequests,
                WaitingCustomer = kpis.WaitingCustomer,
                PaymentFollowUp = kpis.PaymentFollowUp,
                OverdueTasks = kpis.OverdueTasks,
                ActiveProjects = kpis.ActiveProjects
            },
            "Sales KPIs retrieved successfully.");
    }

    public async Task<ServiceResult<DashboardQueueResponseDto>> GetDesignerWorkQueueAsync(
        Guid currentUserId,
        DashboardQueueQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var prepared = await PrepareAsync(currentUserId, query, ApplicationRoles.Designer, cancellationToken);
        if (prepared.Error is not null)
        {
            return prepared.Error;
        }

        var rows = await _dashboard.GetDesignerQueueRowsAsync(prepared.Filter!, cancellationToken);
        var items = rows
            .Select(row => MapDesignerItem(row, prepared.UtcNow))
            .Where(item => MatchesGroupAndPriority(item, prepared.Query!))
            .ToList();

        return ServiceResult<DashboardQueueResponseDto>.Success(
            BuildQueueResponse(items, prepared.Query!),
            "Designer work queue retrieved successfully.");
    }

    public async Task<ServiceResult<DesignerDashboardKpisDto>> GetDesignerKpisAsync(
        Guid currentUserId,
        DashboardQueueQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var prepared = await PrepareAsync(currentUserId, query, ApplicationRoles.Designer, cancellationToken);
        if (prepared.Error is not null)
        {
            return MapError<DesignerDashboardKpisDto>(prepared.Error);
        }

        var kpis = await _dashboard.GetDesignerKpisAsync(prepared.Filter!, cancellationToken);
        return ServiceResult<DesignerDashboardKpisDto>.Success(
            new DesignerDashboardKpisDto
            {
                MeasurementDue = kpis.MeasurementDue,
                ProposalsInProgress = kpis.ProposalsInProgress,
                RevisionRequested = kpis.RevisionRequested,
                OverdueTasks = kpis.OverdueTasks
            },
            "Designer KPIs retrieved successfully.");
    }

    public async Task<ServiceResult<DashboardQueueResponseDto>> GetProductionQueueAsync(
        Guid currentUserId,
        DashboardQueueQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var prepared = await PrepareAsync(currentUserId, query, ApplicationRoles.Production, cancellationToken);
        if (prepared.Error is not null)
        {
            return prepared.Error;
        }

        var rows = await _dashboard.GetProductionQueueRowsAsync(prepared.Filter!, cancellationToken);
        var items = rows
            .Select(row => MapProductionItem(row, prepared.UtcNow))
            .Where(item => MatchesGroupAndPriority(item, prepared.Query!))
            .ToList();

        return ServiceResult<DashboardQueueResponseDto>.Success(
            BuildQueueResponse(items, prepared.Query!),
            "Production queue retrieved successfully.");
    }

    public async Task<ServiceResult<ProductionDashboardKpisDto>> GetProductionKpisAsync(
        Guid currentUserId,
        DashboardQueueQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var prepared = await PrepareAsync(currentUserId, query, ApplicationRoles.Production, cancellationToken);
        if (prepared.Error is not null)
        {
            return MapError<ProductionDashboardKpisDto>(prepared.Error);
        }

        var kpis = await _dashboard.GetProductionKpisAsync(prepared.Filter!, cancellationToken);
        return ServiceResult<ProductionDashboardKpisDto>.Success(
            new ProductionDashboardKpisDto
            {
                PendingReview = kpis.PendingReview,
                InProduction = kpis.InProduction,
                ReadyToComplete = kpis.ReadyToComplete,
                OverdueTasks = kpis.OverdueTasks
            },
            "Production KPIs retrieved successfully.");
    }

    private async Task<PreparedRequest> PrepareAsync(
        Guid currentUserId,
        DashboardQueueQueryDto? query,
        string requiredRole,
        CancellationToken cancellationToken)
    {
        if (currentUserId == Guid.Empty)
        {
            return new PreparedRequest(
                ServiceResult<DashboardQueueResponseDto>.Unauthorized());
        }

        query ??= new DashboardQueueQueryDto();
        if (query.Page < 1 || query.Limit < 1 || query.Limit > MaxLimit)
        {
            return new PreparedRequest(
                ServiceResult<DashboardQueueResponseDto>.BadRequest(
                    $"Page must be >= 1 and limit must be between 1 and {MaxLimit}."));
        }

        if (!IsValidScope(query.Scope))
        {
            return new PreparedRequest(
                ServiceResult<DashboardQueueResponseDto>.BadRequest(
                    "Scope must be mine, team, or all."));
        }

        if (!string.IsNullOrWhiteSpace(query.DateRange) && !IsValidDateRange(query.DateRange))
        {
            return new PreparedRequest(
                ServiceResult<DashboardQueueResponseDto>.BadRequest(
                    "DateRange must be today, thisWeek, or thisMonth."));
        }

        if (!string.IsNullOrWhiteSpace(query.Priority) && !IsValidPriority(query.Priority))
        {
            return new PreparedRequest(
                ServiceResult<DashboardQueueResponseDto>.BadRequest(
                    "Priority must be HIGH, MEDIUM, or LOW."));
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanAccessRoleQueue(roleName, requiredRole))
        {
            return new PreparedRequest(
                ServiceResult<DashboardQueueResponseDto>.Forbidden(
                    "You do not have permission to view this dashboard queue."));
        }

        var utcNow = DateTime.UtcNow;
        var filter = new DashboardQueueFilterReadModel
        {
            Scope = string.IsNullOrWhiteSpace(query.Scope) ? ScopeMine : query.Scope.Trim(),
            CurrentUserId = currentUserId,
            CurrentUserRole = roleName,
            Search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim(),
            DateRange = string.IsNullOrWhiteSpace(query.DateRange) ? null : query.DateRange.Trim(),
            UtcNow = utcNow
        };

        return new PreparedRequest(null, query, filter, utcNow);
    }

    private static DashboardQueueItemDto MapSalesItem(DashboardProjectQueueRowReadModel row, DateTime utcNow)
    {
        var dueAt = DashboardDueHelper.ToDueAtUtc(row.TargetCompletionDate);
        var dueBucket = DashboardDueHelper.ResolveDueBucket(dueAt, utcNow);
        var next = DashboardNextActionResolver.ResolveSales(
            row.Status,
            row.ProjectId,
            row.OrderId,
            row.OrderStatus,
            row.RemainingAmount,
            row.CustomerConfirmedDeliveryAt,
            dueBucket);

        var lastUpdated = row.OrderUpdatedAt ?? row.UpdatedAt ?? row.SubmittedAt ?? row.CreatedAt ?? utcNow;

        return new DashboardQueueItemDto
        {
            Id = row.ProjectId.ToString("D"),
            ProjectId = row.ProjectId,
            ProjectCode = row.ProjectCode,
            ProjectName = row.ProjectName,
            CustomerName = row.CustomerName,
            AssigneeName = row.AssignedSalesName,
            Group = next.Group,
            Phase = next.Phase,
            Status = row.Status?.ToString() ?? string.Empty,
            Priority = next.Priority,
            Action = next.Action,
            ActionPath = next.ActionPath,
            DueAt = dueAt,
            DueBucket = dueBucket,
            Warning = next.Warning,
            LastUpdatedAt = DateTime.SpecifyKind(lastUpdated, DateTimeKind.Utc)
        };
    }

    private static DashboardQueueItemDto MapDesignerItem(DashboardProjectQueueRowReadModel row, DateTime utcNow)
    {
        var dueAt = DashboardDueHelper.ToDueAtUtc(row.TargetCompletionDate);
        var dueBucket = DashboardDueHelper.ResolveDueBucket(dueAt, utcNow);
        var next = DashboardNextActionResolver.ResolveDesigner(row.Status, row.ProjectId, dueBucket);
        var lastUpdated = row.UpdatedAt ?? row.SubmittedAt ?? row.CreatedAt ?? utcNow;

        return new DashboardQueueItemDto
        {
            Id = row.ProjectId.ToString("D"),
            ProjectId = row.ProjectId,
            ProjectCode = row.ProjectCode,
            ProjectName = row.ProjectName,
            CustomerName = row.CustomerName,
            AssigneeName = row.AssignedDesignerName,
            Group = next.Group,
            Phase = next.Phase,
            Status = row.Status?.ToString() ?? string.Empty,
            Priority = next.Priority,
            Action = next.Action,
            ActionPath = next.ActionPath,
            DueAt = dueAt,
            DueBucket = dueBucket,
            Warning = next.Warning,
            LastUpdatedAt = DateTime.SpecifyKind(lastUpdated, DateTimeKind.Utc)
        };
    }

    private static DashboardQueueItemDto MapProductionItem(
        DashboardProductionQueueRowReadModel row,
        DateTime utcNow)
    {
        var dueAt = DashboardDueHelper.ToDueAtUtc(row.EstimatedCompletionDate);
        var dueBucket = DashboardDueHelper.ResolveDueBucket(dueAt, utcNow);
        var next = DashboardNextActionResolver.ResolveProduction(
            row.Status,
            row.ProductionRequestId,
            row.ProjectId,
            dueBucket,
            row.BlockedItemCount);
        var lastUpdated = row.UpdatedAt ?? row.CreatedAt ?? utcNow;

        var priority = string.IsNullOrWhiteSpace(row.Priority)
            ? next.Priority
            : NormalizeStoredPriority(row.Priority) ?? next.Priority;

        if (string.Equals(dueBucket, DueBucketOverdue, StringComparison.Ordinal))
        {
            priority = PriorityHigh;
        }

        return new DashboardQueueItemDto
        {
            Id = row.ProductionRequestId.ToString("D"),
            ProjectId = row.ProjectId,
            ProjectCode = row.ProjectCode,
            ProjectName = row.ProjectName,
            CustomerName = row.CustomerName,
            AssigneeName = row.AssignedToName,
            Group = next.Group,
            Phase = next.Phase,
            Status = row.Status.ToString(),
            Priority = priority,
            Action = next.Action,
            ActionPath = next.ActionPath,
            DueAt = dueAt,
            DueBucket = dueBucket,
            Warning = next.Warning,
            LastUpdatedAt = DateTime.SpecifyKind(lastUpdated, DateTimeKind.Utc)
        };
    }

    private static DashboardQueueResponseDto BuildQueueResponse(
        IReadOnlyList<DashboardQueueItemDto> items,
        DashboardQueueQueryDto query)
    {
        var counts = items
            .GroupBy(item => item.Group, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        var page = query.Page < 1 ? DefaultPage : query.Page;
        var limit = query.Limit < 1 ? DefaultLimit : Math.Min(query.Limit, MaxLimit);
        var paged = items
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToList();

        return new DashboardQueueResponseDto
        {
            Items = paged,
            CountsByGroup = counts,
            Page = page,
            Limit = limit,
            Total = items.Count
        };
    }

    private static bool MatchesGroupAndPriority(DashboardQueueItemDto item, DashboardQueueQueryDto query)
    {
        if (!string.IsNullOrWhiteSpace(query.Group) &&
            !string.Equals(item.Group, query.Group.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.Priority) &&
            !string.Equals(item.Priority, query.Priority.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static bool CanAccessRoleQueue(string? roleName, string requiredRole)
    {
        if (string.Equals(roleName, ApplicationRoles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(roleName, requiredRole, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsValidScope(string? scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            return true;
        }

        return string.Equals(scope, ScopeMine, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(scope, ScopeTeam, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(scope, ScopeAll, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsValidDateRange(string dateRange)
    {
        return string.Equals(dateRange, DateRangeToday, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(dateRange, DateRangeThisWeek, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(dateRange, DateRangeThisMonth, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsValidPriority(string priority)
    {
        return string.Equals(priority, PriorityHigh, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(priority, PriorityMedium, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(priority, PriorityLow, StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeStoredPriority(string? priority)
    {
        if (string.IsNullOrWhiteSpace(priority))
        {
            return null;
        }

        if (string.Equals(priority, PriorityHigh, StringComparison.OrdinalIgnoreCase))
        {
            return PriorityHigh;
        }

        if (string.Equals(priority, PriorityMedium, StringComparison.OrdinalIgnoreCase))
        {
            return PriorityMedium;
        }

        if (string.Equals(priority, PriorityLow, StringComparison.OrdinalIgnoreCase))
        {
            return PriorityLow;
        }

        return null;
    }

    private static ServiceResult<T> MapError<T>(ServiceResult<DashboardQueueResponseDto> error)
    {
        return new ServiceResult<T>(error.Status, error.Message ?? "Request failed")
        {
            ErrorCode = error.ErrorCode,
            Errors = error.Errors
        };
    }

    private sealed record PreparedRequest(
        ServiceResult<DashboardQueueResponseDto>? Error,
        DashboardQueueQueryDto? Query = null,
        DashboardQueueFilterReadModel? Filter = null,
        DateTime UtcNow = default);
}
