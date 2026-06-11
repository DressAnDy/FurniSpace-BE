using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.DTOs.Accounts;
using FurniSpace.Infrastructure.Repositories.Base;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Infrastructure.Repositories.Repository;

public sealed class AccountRepository : GenericRepository<Account>, IAccountRepository
{
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

    private static string BuildSearchPattern(string search)
    {
        return $"%{search.Trim()}%";
    }
}
