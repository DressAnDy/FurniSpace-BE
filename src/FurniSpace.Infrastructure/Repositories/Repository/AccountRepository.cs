using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.Data;
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
            var value = search.Trim().ToLower();
            query = query.Where(account =>
                account.Email.ToLower().Contains(value) ||
                account.FullName.ToLower().Contains(value) ||
                (account.Phone != null && account.Phone.ToLower().Contains(value)));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(account => account.Status == status);
        }

        return query;
    }
}
