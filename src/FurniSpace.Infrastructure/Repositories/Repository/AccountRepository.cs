using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.ReadModels.Accounts;
using FurniSpace.Infrastructure.Repositories.Base;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Infrastructure.Repositories.Repository;

public sealed class AccountRepository : GenericRepository<Account>, IAccountRepository
{
    private const string DesignerRoleName = "DESIGNER";

    private static readonly ProjectStatus[] ActiveDesignerProjectStatuses =
    [
        ProjectStatus.IN_CONSULTATION,
        ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT,
        ProjectStatus.MEASUREMENT_REQUIRED,
        ProjectStatus.SPACE_VERIFIED,
        ProjectStatus.PROPOSAL_CONSULTING,
        ProjectStatus.PROPOSAL_SELECTED,
        ProjectStatus.QUOTATION_SENT,
        ProjectStatus.ORDER_CONFIRMED,
        ProjectStatus.IN_PRODUCTION,
        ProjectStatus.PRODUCTION_BLOCKED,
        ProjectStatus.READY_FOR_DELIVERY,
        ProjectStatus.DELIVERING,
        ProjectStatus.DELIVERED
    ];

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
        return await BuildAvailableDesignerQuery(maxActiveProjects, search)
            .OrderBy(designer => designer.CurrentActiveProjectCount)
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
        return BuildAvailableDesignerQuery(maxActiveProjects, search).CountAsync(cancellationToken);
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

    private IQueryable<AvailableDesignerReadModel> BuildAvailableDesignerQuery(
        int maxActiveProjects,
        string? search)
    {
        var query =
            from account in Query()
            join role in DbContext.RoleSet on account.RoleId equals role.RoleId
            where role.RoleName == DesignerRoleName &&
                account.Status == AccountStatus.ACTIVE &&
                account.DeletedAt == null
            let activeProjectCount = DbContext.ProjectSet.Count(project =>
                project.AssignedDesignerId == account.AccountId &&
                project.Status.HasValue &&
                ActiveDesignerProjectStatuses.Contains(project.Status.Value))
            // Capacity filtering is temporarily disabled so Sales/Admin can still see
            // designers who already have two or more active projects.
            // where activeProjectCount < maxActiveProjects
            select new AvailableDesignerReadModel
            {
                AccountId = account.AccountId,
                Email = account.Email,
                FullName = account.FullName,
                Phone = account.Phone,
                AvatarUrl = account.AvatarUrl,
                Status = account.Status,
                CurrentActiveProjectCount = activeProjectCount,
                MaxActiveProjects = maxActiveProjects,
                AvailableSlot = maxActiveProjects - activeProjectCount,
                CreatedAt = account.CreatedAt,
                UpdatedAt = account.UpdatedAt
            };

        if (string.IsNullOrWhiteSpace(search))
        {
            return query;
        }

        var pattern = BuildSearchPattern(search);
        return query.Where(designer =>
            EF.Functions.ILike(designer.Email, pattern) ||
            EF.Functions.ILike(designer.FullName, pattern) ||
            (designer.Phone != null && EF.Functions.ILike(designer.Phone, pattern)));
    }

    private static string BuildSearchPattern(string search)
    {
        return $"%{search.Trim()}%";
    }
}
