using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.DTOs.Accounts;
using FurniSpace.Infrastructure.Repositories.Base;

namespace FurniSpace.Infrastructure.Repositories.IRepository;

public interface IAccountRepository : IGenericRepository<Account>
{
    Task<Account?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<AccountDetailReadModel?> GetDetailAsync(Guid accountId, CancellationToken cancellationToken = default);
    Task<string?> GetRoleNameAsync(Guid roleId, CancellationToken cancellationToken = default);
    Task<Guid?> GetRoleIdByNameAsync(string roleName, CancellationToken cancellationToken = default);
    Task<bool> RoleExistsAsync(Guid roleId, CancellationToken cancellationToken = default);
    Task<bool> EmailExistsAsync(string email, Guid? excludedAccountId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AvailableDesignerReadModel>> GetAvailableDesignersAsync(
        int page,
        int pageSize,
        int maxActiveProjects,
        string? search,
        CancellationToken cancellationToken = default);
    Task<int> CountAvailableDesignersAsync(
        int maxActiveProjects,
        string? search,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Account>> GetPagedAsync(int page, int pageSize, string? search, string? status, bool includeDeleted, CancellationToken cancellationToken = default);
    Task<int> CountAsync(string? search, string? status, bool includeDeleted, CancellationToken cancellationToken = default);
}
