#nullable enable

using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.ReadModels.Production;
using FurniSpace.Infrastructure.Repositories.Base;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Infrastructure.Repositories.Repository;

public sealed class ProductionRequestRepository : GenericRepository<ProductionRequest>, IProductionRequestRepository
{
    private const string ProductionRoleName = "PRODUCTION";

    private static readonly ProductionRequestStatus[] ActiveRequestStatuses =
    [
        ProductionRequestStatus.PENDING_REVIEW,
        ProductionRequestStatus.FEASIBLE,
        ProductionRequestStatus.IN_PRODUCTION
    ];

    private static readonly ProductionRequestStatus[] ScheduleReadRequestStatuses =
    [
        ProductionRequestStatus.PENDING_REVIEW,
        ProductionRequestStatus.FEASIBLE,
        ProductionRequestStatus.IN_PRODUCTION,
        ProductionRequestStatus.COMPLETED
    ];

    public ProductionRequestRepository(AppDbContext dbContext)
        : base(dbContext)
    {
    }

    public Task<bool> HasActiveRequestForOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.ProductionRequestSet.AnyAsync(
            request =>
                request.OrderId == orderId &&
                request.Status.HasValue &&
                ActiveRequestStatuses.Contains(request.Status.Value),
            cancellationToken);
    }

    public Task<bool> ExistsForOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.ProductionRequestSet.AnyAsync(
            request => request.OrderId == orderId,
            cancellationToken);
    }

    public Task<int> CountCreatedOnAsync(
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var start = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var end = start.AddDays(1);
        return DbContext.ProductionRequestSet.CountAsync(
            request => request.CreatedAt >= start && request.CreatedAt < end,
            cancellationToken);
    }

    public Task<List<OrderItem>> GetProductOrderItemsAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.OrderItemSet
            .Where(item => item.OrderId == orderId)
            .OrderBy(item => item.OrderItemId)
            .ToListAsync(cancellationToken);
    }

    public Task AddItemsAsync(
        List<ProductionItem> items,
        CancellationToken cancellationToken = default)
    {
        return DbContext.ProductionItemSet.AddRangeAsync(items, cancellationToken);
    }

    public async Task<bool> IsActiveProductionStaffAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var assignee = await GetAssigneeAsync(accountId, cancellationToken);
        return IsValidActiveProductionAssignee(assignee);
    }

    public Task<ProductionAssigneeReadModel?> GetAssigneeAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        return (
            from account in DbContext.AccountSet
            join role in DbContext.RoleSet on account.RoleId equals role.RoleId
            where account.AccountId == accountId
            select new ProductionAssigneeReadModel
            {
                AccountId = account.AccountId,
                RoleName = role.RoleName,
                Status = account.Status,
                DeletedAt = account.DeletedAt
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<List<AvailableProductionStaffReadModel>> GetAvailableStaffAsync(
        string? search,
        CancellationToken cancellationToken = default)
    {
        var normalizedSearch = search?.Trim();
        var query =
            from account in DbContext.AccountSet
            join role in DbContext.RoleSet on account.RoleId equals role.RoleId
            where role.RoleName == ProductionRoleName &&
                account.Status == AccountStatus.ACTIVE &&
                account.DeletedAt == null
            let activeCount = DbContext.ProductionRequestSet.Count(request =>
                request.AssignedTo == account.AccountId &&
                request.Status.HasValue &&
                ActiveRequestStatuses.Contains(request.Status.Value))
            let pendingReviewCount = DbContext.ProductionRequestSet.Count(request =>
                request.AssignedTo == account.AccountId &&
                request.Status == ProductionRequestStatus.PENDING_REVIEW)
            let inProductionCount = DbContext.ProductionRequestSet.Count(request =>
                request.AssignedTo == account.AccountId &&
                request.Status == ProductionRequestStatus.IN_PRODUCTION)
            select new AvailableProductionStaffReadModel
            {
                AccountId = account.AccountId,
                FullName = account.FullName,
                Email = account.Email,
                AvatarUrl = account.AvatarUrl,
                AccountStatus = account.Status,
                ActiveRequestCount = activeCount,
                PendingReviewRequestCount = pendingReviewCount,
                InProductionRequestCount = inProductionCount,
                BlockedRequestCount = 0,
                IsAvailable = true
            };

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            query = DbContext.Database.IsRelational()
                ? ApplyRelationalStaffSearch(query, normalizedSearch)
                : ApplyInMemoryStaffSearch(query, normalizedSearch);
        }

        return query
            .OrderBy(staff => staff.ActiveRequestCount)
            .ThenBy(staff => staff.FullName)
            .ThenBy(staff => staff.Email)
            .ToListAsync(cancellationToken);
    }

    private static IQueryable<AvailableProductionStaffReadModel> ApplyRelationalStaffSearch(
        IQueryable<AvailableProductionStaffReadModel> query,
        string search)
    {
        var pattern = $"%{search}%";
        return query.Where(staff =>
            EF.Functions.ILike(staff.FullName, pattern) ||
            EF.Functions.ILike(staff.Email, pattern));
    }

    private static IQueryable<AvailableProductionStaffReadModel> ApplyInMemoryStaffSearch(
        IQueryable<AvailableProductionStaffReadModel> query,
        string search)
    {
        return query.Where(staff =>
            StaffFieldContains(staff.FullName, search) ||
            StaffFieldContains(staff.Email, search));
    }

    private static bool StaffFieldContains(string? value, string search)
    {
        return value?.Contains(search, StringComparison.OrdinalIgnoreCase) == true;
    }

    public Task<List<ProductionRequestListItemReadModel>> GetQueueAsync(
        ProductionRequestQueueReadModel query,
        CancellationToken cancellationToken = default)
    {
        return ApplyQueueFilters(BuildQueueQuery(), query)
            .OrderByDescending(request => request.CreatedAt)
            .ThenBy(request => request.ProductionCode)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> HasViewableAssignedRequestAsync(
        Guid projectId,
        Guid productionAccountId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.ProductionRequestSet.AnyAsync(
            request =>
                request.ProjectId == projectId &&
                request.AssignedTo == productionAccountId &&
                request.Status.HasValue &&
                ScheduleReadRequestStatuses.Contains(request.Status.Value),
            cancellationToken);
    }

    public async Task<ProductionRequestDetailReadModel?> GetDetailAsync(
        Guid productionRequestId,
        CancellationToken cancellationToken = default)
    {
        var detail = await BuildDetailQuery()
            .FirstOrDefaultAsync(request => request.ProductionRequestId == productionRequestId, cancellationToken);
        if (detail is null)
        {
            return null;
        }

        detail.Items = await DbContext.ProductionItemSet
            .Where(item => item.ProductionRequestId == productionRequestId)
            .GroupJoin(
                DbContext.OrderItemSet,
                item => item.OrderItemId,
                orderItem => orderItem.OrderItemId,
                (item, orderItems) => new { item, orderItems })
            .SelectMany(
                pair => pair.orderItems.DefaultIfEmpty(),
                (pair, orderItem) => new ProductionItemReadModel
                {
                    ProductionItemId = pair.item.ProductionItemId,
                    ProductionRequestId = pair.item.ProductionRequestId,
                    OrderItemId = pair.item.OrderItemId,
                    ProductVersionId = pair.item.ProductVersionId,
                    ProductNameSnapshot = pair.item.ProductNameSnapshot,
                    ProductVersionNameSnapshot = pair.item.ProductVersionNameSnapshot,
                    Quantity = pair.item.Quantity,
                    Status = pair.item.Status,
                    MaterialNote = pair.item.MaterialNote,
                    ProductionNote = pair.item.ProductionNote,
                    EstimatedCompletionDate = pair.item.EstimatedCompletionDate,
                    StartedAt = pair.item.StartedAt,
                    CompletedAt = pair.item.CompletedAt,
                    OrderItemStatus = orderItem == null ? null : orderItem.Status
                })
            .OrderBy(item => item.ProductNameSnapshot)
            .ThenBy(item => item.ProductionItemId)
            .ToListAsync(cancellationToken);

        return detail;
    }

    public Task<ProductionItem?> GetItemByIdAsync(
        Guid productionItemId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.ProductionItemSet.FirstOrDefaultAsync(
            item => item.ProductionItemId == productionItemId,
            cancellationToken);
    }

    public Task<List<ProductionItem>> GetItemsByRequestIdAsync(
        Guid productionRequestId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.ProductionItemSet
            .Where(item => item.ProductionRequestId == productionRequestId)
            .OrderBy(item => item.ProductionItemId)
            .ToListAsync(cancellationToken);
    }

    public async Task<ProductionRequestDetailReadModel?> GetDetailByItemIdAsync(
        Guid productionItemId,
        CancellationToken cancellationToken = default)
    {
        var productionRequestId = await DbContext.ProductionItemSet
            .Where(item => item.ProductionItemId == productionItemId)
            .Select(item => (Guid?)item.ProductionRequestId)
            .FirstOrDefaultAsync(cancellationToken);

        return productionRequestId.HasValue
            ? await GetDetailAsync(productionRequestId.Value, cancellationToken)
            : null;
    }

    public void UpdateItem(ProductionItem item)
    {
        DbContext.ProductionItemSet.Update(item);
    }

    private IQueryable<ProductionRequestListItemReadModel> BuildQueueQuery()
    {
        return from request in DbContext.ProductionRequestSet
               join project in DbContext.ProjectSet on request.ProjectId equals project.ProjectId
               join order in DbContext.OrderSet on request.OrderId equals order.OrderId
               join assignee in DbContext.AccountSet on request.AssignedTo equals assignee.AccountId into assignees
               from assignee in assignees.DefaultIfEmpty()
               select new ProductionRequestListItemReadModel
               {
                   ProductionRequestId = request.ProductionRequestId,
                   ProductionCode = request.ProductionCode,
                   ProjectId = request.ProjectId,
                   ProjectCode = project.ProjectCode,
                   ProjectName = project.ProjectName,
                   AssignedSalesId = project.AssignedSalesId,
                   OrderId = request.OrderId,
                   OrderCode = order.OrderCode,
                   AssignedTo = request.AssignedTo,
                   AssignedToName = assignee == null ? null : assignee.FullName,
                   Status = request.Status,
                   Priority = request.Priority,
                   EstimatedStartDate = request.EstimatedStartDate,
                   EstimatedCompletionDate = request.EstimatedCompletionDate,
                   ProductionItemCount = DbContext.ProductionItemSet.Count(item =>
                       item.ProductionRequestId == request.ProductionRequestId),
                   CreatedAt = request.CreatedAt,
                   UpdatedAt = request.UpdatedAt
               };
    }

    private IQueryable<ProductionRequestDetailReadModel> BuildDetailQuery()
    {
        return from request in DbContext.ProductionRequestSet
               join project in DbContext.ProjectSet on request.ProjectId equals project.ProjectId
               join order in DbContext.OrderSet on request.OrderId equals order.OrderId
               join assignee in DbContext.AccountSet on request.AssignedTo equals assignee.AccountId into assignees
               from assignee in assignees.DefaultIfEmpty()
               select new ProductionRequestDetailReadModel
               {
                   ProductionRequestId = request.ProductionRequestId,
                   ProductionCode = request.ProductionCode,
                   ProjectId = request.ProjectId,
                   ProjectCode = project.ProjectCode,
                   ProjectName = project.ProjectName,
                   AssignedSalesId = project.AssignedSalesId,
                   OrderId = request.OrderId,
                   OrderCode = order.OrderCode,
                   AssignedTo = request.AssignedTo,
                   AssignedToName = assignee == null ? null : assignee.FullName,
                   Status = request.Status,
                   Priority = request.Priority,
                   EstimatedStartDate = request.EstimatedStartDate,
                   EstimatedCompletionDate = request.EstimatedCompletionDate,
                   ActualStartDate = request.ActualStartDate,
                   ActualCompletionDate = request.ActualCompletionDate,
                   CancellationReason = request.CancellationReason,
                   Note = request.Note,
                   CreatedAt = request.CreatedAt,
                   UpdatedAt = request.UpdatedAt
               };
    }

    private static IQueryable<ProductionRequestListItemReadModel> ApplyQueueFilters(
        IQueryable<ProductionRequestListItemReadModel> query,
        ProductionRequestQueueReadModel filter)
    {
        query = filter.CurrentUserRole switch
        {
            "ADMIN" or ProductionRoleName => query,
            "SALES" => query.Where(request => request.AssignedSalesId == filter.CurrentUserId),
            _ => query.Where(_ => false)
        };

        if (filter.Status.HasValue)
        {
            query = query.Where(request => request.Status == filter.Status.Value);
        }

        if (filter.AssignedTo.HasValue)
        {
            query = query.Where(request => request.AssignedTo == filter.AssignedTo.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Priority))
        {
            var priority = filter.Priority.Trim().ToUpperInvariant();
            query = query.Where(request => request.Priority == priority);
        }

        return query;
    }

    private static bool IsValidActiveProductionAssignee(ProductionAssigneeReadModel? assignee)
    {
        return assignee?.RoleName == ProductionRoleName &&
            assignee.Status == AccountStatus.ACTIVE &&
            assignee.DeletedAt is null;
    }

    public async Task<DateOnly?> GetMaxOperationalProductionDateAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var requests = await DbContext.ProductionRequestSet
            .AsNoTracking()
            .Where(request => request.ProjectId == projectId)
            .Select(request => new
            {
                request.EstimatedStartDate,
                request.EstimatedCompletionDate,
                request.ActualStartDate,
                request.ActualCompletionDate
            })
            .ToListAsync(cancellationToken);

        DateOnly? maxDate = null;
        foreach (var request in requests)
        {
            maxDate = MaxDateOnly(maxDate, request.EstimatedStartDate);
            maxDate = MaxDateOnly(maxDate, request.EstimatedCompletionDate);
            maxDate = MaxDateOnly(maxDate, request.ActualStartDate);
            maxDate = MaxDateOnly(maxDate, request.ActualCompletionDate);
        }

        return maxDate;
    }

    private static DateOnly? MaxDateOnly(DateOnly? current, DateOnly? candidate)
    {
        if (!candidate.HasValue)
        {
            return current;
        }

        return !current.HasValue || candidate.Value > current.Value
            ? candidate
            : current;
    }
}
