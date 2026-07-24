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
        ProductionRequestStatus.IN_PRODUCTION,
        ProductionRequestStatus.BLOCKED
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
            .Where(item => item.OrderId == orderId && item.ItemType == QuotationItemType.PRODUCT_ITEM)
            .OrderBy(item => item.OrderItemId)
            .ToListAsync(cancellationToken);
    }

    public Task AddItemsAsync(
        List<ProductionItem> items,
        CancellationToken cancellationToken = default)
    {
        return DbContext.ProductionItemSet.AddRangeAsync(items, cancellationToken);
    }

    public Task<bool> IsActiveProductionStaffAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        return (
            from account in DbContext.AccountSet
            join role in DbContext.RoleSet on account.RoleId equals role.RoleId
            where account.AccountId == accountId &&
                account.Status == AccountStatus.ACTIVE &&
                account.DeletedAt == null &&
                role.RoleName == ProductionRoleName
            select account.AccountId)
            .AnyAsync(cancellationToken);
    }

    public Task<List<AvailableProductionStaffReadModel>> GetAvailableStaffAsync(
        string? search,
        CancellationToken cancellationToken = default)
    {
        var normalizedSearch = search?.Trim().ToLowerInvariant();
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
            let blockedCount = DbContext.ProductionRequestSet.Count(request =>
                request.AssignedTo == account.AccountId &&
                request.Status == ProductionRequestStatus.BLOCKED)
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
                BlockedRequestCount = blockedCount,
                IsAvailable = true
            };

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            query = query.Where(staff =>
                staff.FullName.ToLower().Contains(normalizedSearch) ||
                staff.Email.ToLower().Contains(normalizedSearch));
        }

        return query
            .OrderBy(staff => staff.ActiveRequestCount)
            .ThenBy(staff => staff.FullName)
            .ThenBy(staff => staff.Email)
            .ToListAsync(cancellationToken);
    }
}
