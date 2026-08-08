using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Accounts;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.ReadModels.Accounts;
using FurniSpace.Infrastructure.Repositories.Base;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Infrastructure.Repositories.Repository;

public sealed class AccountRepository : GenericRepository<Account>, IAccountRepository
{
    private const string DesignerRoleName = "DESIGNER";
    private const string SortDesignActiveCountDesc = "DesignActiveCountDesc";
    private const string SortAvailableSlotDesc = "AvailableSlotDesc";

    public AccountRepository(AppDbContext dbContext) : base(dbContext)
    {
    }

    public Task<Account?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return DbSet.FirstOrDefaultAsync(account => account.Email == email, cancellationToken);
    }

    public Task<AccountDetailReadModel?> GetDetailAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        return (
            from account in Query()
            join role in DbContext.RoleSet on account.RoleId equals role.RoleId
            where account.AccountId == accountId
            select new AccountDetailReadModel
            {
                AccountId = account.AccountId,
                Email = account.Email,
                FullName = account.FullName,
                Phone = account.Phone,
                AvatarUrl = account.AvatarUrl,
                Role = new AccountRoleReadModel
                {
                    RoleId = role.RoleId,
                    RoleName = role.RoleName,
                    Description = role.Description
                },
                Status = account.Status,
                CreatedAt = account.CreatedAt,
                UpdatedAt = account.UpdatedAt,
                DeletedAt = account.DeletedAt
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<string?> GetRoleNameAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        return DbContext.RoleSet
            .Where(role => role.RoleId == roleId)
            .Select(role => role.RoleName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<Guid?> GetRoleIdByNameAsync(string roleName, CancellationToken cancellationToken = default)
    {
        return DbContext.RoleSet
            .Where(role => role.RoleName == roleName)
            .Select(role => (Guid?)role.RoleId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<bool> RoleExistsAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        return DbContext.RoleSet.AnyAsync(role => role.RoleId == roleId, cancellationToken);
    }

    public Task<bool> EmailExistsAsync(string email, Guid? excludedAccountId = null, CancellationToken cancellationToken = default)
    {
        return DbSet.AnyAsync(
            account => account.Email == email &&
                (!excludedAccountId.HasValue || account.AccountId != excludedAccountId.Value),
            cancellationToken);
    }

    public async Task<IReadOnlyList<AvailableDesignerReadModel>> GetAvailableDesignersAsync(
        int page,
        int pageSize,
        int maxActiveProjects,
        string? search,
        CancellationToken cancellationToken = default)
    {
        return await BuildDesignerWorkloadQuery(maxActiveProjects, search, capacityState: null)
            .OrderBy(designer => designer.DesignActiveCount)
            .ThenBy(designer => designer.FullName)
            .ThenBy(designer => designer.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAvailableDesignersAsync(
        int maxActiveProjects,
        string? search,
        CancellationToken cancellationToken = default)
    {
        return BuildDesignerWorkloadQuery(maxActiveProjects, search, capacityState: null)
            .CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AvailableDesignerReadModel>> GetDesignerWorkloadAsync(
        int page,
        int pageSize,
        int maxActiveProjects,
        string? search,
        string? capacityState,
        string sortBy,
        CancellationToken cancellationToken = default)
    {
        var query = BuildDesignerWorkloadQuery(maxActiveProjects, search, capacityState);
        query = ApplyWorkloadSort(query, sortBy);

        return await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountDesignerWorkloadAsync(
        int maxActiveProjects,
        string? search,
        string? capacityState,
        CancellationToken cancellationToken = default)
    {
        return BuildDesignerWorkloadQuery(maxActiveProjects, search, capacityState)
            .CountAsync(cancellationToken);
    }

    public async Task<DesignerWorkloadSummaryReadModel> GetDesignerWorkloadSummaryAsync(
        int maxActiveProjects,
        CancellationToken cancellationToken = default)
    {
        var designers = BuildDesignerWorkloadQuery(maxActiveProjects, search: null, capacityState: null);

        var summary = await designers
            .GroupBy(_ => 1)
            .Select(group => new DesignerWorkloadSummaryReadModel
            {
                TotalActiveDesigners = group.Count(),
                AvailableCount = group.Count(designer => designer.CapacityState == DesignerWorkloadStatusSets.CapacityAvailable),
                FullCount = group.Count(designer => designer.CapacityState == DesignerWorkloadStatusSets.CapacityFull),
                OverCount = group.Count(designer => designer.CapacityState == DesignerWorkloadStatusSets.CapacityOver),
                TotalDesignActiveProjects = group.Sum(designer => designer.DesignActiveCount)
            })
            .FirstOrDefaultAsync(cancellationToken);

        return summary ?? new DesignerWorkloadSummaryReadModel();
    }

    public async Task<IReadOnlyList<DesignerAssignedProjectReadModel>> GetDesignerAssignedProjectsAsync(
        Guid designerId,
        int page,
        int pageSize,
        string? bucket,
        CancellationToken cancellationToken = default)
    {
        return await BuildDesignerAssignedProjectsQuery(designerId, bucket)
            .OrderByDescending(project => project.DesignerAssignedAt)
            .ThenByDescending(project => project.ProjectCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountDesignerAssignedProjectsAsync(
        Guid designerId,
        string? bucket,
        CancellationToken cancellationToken = default)
    {
        return BuildDesignerAssignedProjectsQuery(designerId, bucket).CountAsync(cancellationToken);
    }

    public Task<bool> IsActiveDesignerAsync(Guid designerId, CancellationToken cancellationToken = default)
    {
        return (
            from account in Query()
            join role in DbContext.RoleSet on account.RoleId equals role.RoleId
            where account.AccountId == designerId &&
                role.RoleName == DesignerRoleName &&
                account.Status == AccountStatus.ACTIVE &&
                account.DeletedAt == null
            select account.AccountId)
            .AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Account>> GetPagedAsync(
        int page,
        int pageSize,
        string? search,
        string? status,
        bool includeDeleted,
        CancellationToken cancellationToken = default)
    {
        return await BuildQuery(search, status, includeDeleted)
            .OrderByDescending(account => account.CreatedAt)
            .ThenBy(account => account.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAsync(string? search, string? status, bool includeDeleted, CancellationToken cancellationToken = default)
    {
        return BuildQuery(search, status, includeDeleted).CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AccountFacetCountReadModel>> CountGroupedByStatusAsync(
        bool includeDeleted,
        CancellationToken cancellationToken = default)
    {
        return await BuildQuery(search: null, status: null, includeDeleted)
            .GroupBy(account => account.Status)
            .Select(group => new AccountFacetCountReadModel
            {
                Key = group.Key == null ? "UNKNOWN" : group.Key.ToString()!,
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AccountFacetCountReadModel>> CountGroupedByRoleIdAsync(
        bool includeDeleted,
        CancellationToken cancellationToken = default)
    {
        return await BuildQuery(search: null, status: null, includeDeleted)
            .GroupBy(account => account.RoleId)
            .Select(group => new AccountFacetCountReadModel
            {
                Key = group.Key.ToString(),
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);
    }

    private IQueryable<Account> BuildQuery(string? search, string? status, bool includeDeleted)
    {
        var query = Query();

        if (!includeDeleted)
        {
            query = query.Where(account => account.DeletedAt == null);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = BuildSearchPattern(search);
            query = query.Where(account =>
                EF.Functions.ILike(account.Email, pattern) ||
                EF.Functions.ILike(account.FullName, pattern) ||
                (account.Phone != null && EF.Functions.ILike(account.Phone, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<AccountStatus>(status, ignoreCase: true, out var accountStatus))
        {
            query = query.Where(account => account.Status == accountStatus);
        }

        return query;
    }

    private IQueryable<AvailableDesignerReadModel> BuildDesignerWorkloadQuery(
        int maxActiveProjects,
        string? search,
        string? capacityState)
    {
        var designActiveStatuses = DesignerWorkloadStatusSets.DesignActive;
        var lifecycleStatuses = DesignerWorkloadStatusSets.LifecycleAssigned;

        var query =
            from account in Query()
            join role in DbContext.RoleSet on account.RoleId equals role.RoleId
            where role.RoleName == DesignerRoleName &&
                account.Status == AccountStatus.ACTIVE &&
                account.DeletedAt == null
            let designActiveCount = DbContext.ProjectSet.Count(project =>
                project.AssignedDesignerId == account.AccountId &&
                project.Status.HasValue &&
                designActiveStatuses.Contains(project.Status.Value))
            let lifecycleAssignedCount = DbContext.ProjectSet.Count(project =>
                project.AssignedDesignerId == account.AccountId &&
                project.Status.HasValue &&
                lifecycleStatuses.Contains(project.Status.Value))
            select new AvailableDesignerReadModel
            {
                AccountId = account.AccountId,
                Email = account.Email,
                FullName = account.FullName,
                Phone = account.Phone,
                AvatarUrl = account.AvatarUrl,
                Status = account.Status,
                DesignActiveCount = designActiveCount,
                LifecycleAssignedCount = lifecycleAssignedCount,
                CurrentActiveProjectCount = designActiveCount,
                MaxActiveProjects = maxActiveProjects,
                AvailableSlot = maxActiveProjects - designActiveCount,
                CapacityState = designActiveCount < maxActiveProjects
                    ? DesignerWorkloadStatusSets.CapacityAvailable
                    : designActiveCount == maxActiveProjects
                        ? DesignerWorkloadStatusSets.CapacityFull
                        : DesignerWorkloadStatusSets.CapacityOver,
                CreatedAt = account.CreatedAt,
                UpdatedAt = account.UpdatedAt
            };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = BuildSearchPattern(search);
            query = query.Where(designer =>
                EF.Functions.ILike(designer.Email, pattern) ||
                EF.Functions.ILike(designer.FullName, pattern) ||
                (designer.Phone != null && EF.Functions.ILike(designer.Phone, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(capacityState))
        {
            var normalized = capacityState.Trim().ToUpperInvariant();
            query = query.Where(designer => designer.CapacityState == normalized);
        }

        return query;
    }

    private static IQueryable<AvailableDesignerReadModel> ApplyWorkloadSort(
        IQueryable<AvailableDesignerReadModel> query,
        string sortBy)
    {
        if (string.Equals(sortBy, SortAvailableSlotDesc, StringComparison.OrdinalIgnoreCase))
        {
            return query
                .OrderByDescending(designer => designer.AvailableSlot)
                .ThenBy(designer => designer.FullName)
                .ThenBy(designer => designer.Email);
        }

        return query
            .OrderByDescending(designer => designer.DesignActiveCount)
            .ThenBy(designer => designer.FullName)
            .ThenBy(designer => designer.Email);
    }

    private IQueryable<DesignerAssignedProjectReadModel> BuildDesignerAssignedProjectsQuery(
        Guid designerId,
        string? bucket)
    {
        var projects =
            from project in DbContext.ProjectSet
            where project.AssignedDesignerId == designerId
            join customer in DbContext.AccountSet on project.CustomerId equals customer.AccountId
            join sales in DbContext.AccountSet on project.AssignedSalesId equals sales.AccountId into salesJoin
            from sales in salesJoin.DefaultIfEmpty()
            select new DesignerAssignedProjectReadModel
            {
                ProjectId = project.ProjectId,
                ProjectCode = project.ProjectCode,
                ProjectName = project.ProjectName,
                Status = project.Status,
                DesignerAssignedAt = project.DesignerAssignedAt,
                CustomerId = project.CustomerId,
                CustomerName = customer.FullName,
                AssignedSalesId = project.AssignedSalesId,
                SalesName = sales != null ? sales.FullName : null
            };

        if (string.IsNullOrWhiteSpace(bucket))
        {
            return projects;
        }

        var normalized = bucket.Trim().ToUpperInvariant();
        return normalized switch
        {
            DesignerWorkloadStatusSets.BucketDesignActive => projects.Where(project =>
                project.Status.HasValue &&
                DesignerWorkloadStatusSets.DesignActive.Contains(project.Status.Value)),
            DesignerWorkloadStatusSets.BucketPostDesign => projects.Where(project =>
                project.Status.HasValue &&
                DesignerWorkloadStatusSets.PostDesign.Contains(project.Status.Value)),
            DesignerWorkloadStatusSets.BucketTerminal => projects.Where(project =>
                project.Status.HasValue &&
                DesignerWorkloadStatusSets.Terminal.Contains(project.Status.Value)),
            DesignerWorkloadStatusSets.BucketOther => projects.Where(project =>
                !project.Status.HasValue ||
                (
                    !DesignerWorkloadStatusSets.DesignActive.Contains(project.Status.Value) &&
                    !DesignerWorkloadStatusSets.PostDesign.Contains(project.Status.Value) &&
                    !DesignerWorkloadStatusSets.Terminal.Contains(project.Status.Value)
                )),
            _ => projects.Where(_ => false)
        };
    }

    private static string BuildSearchPattern(string search)
    {
        return $"%{search.Trim()}%";
    }
}
