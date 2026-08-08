using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.ReadModels.Accounts;
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
    Task<IReadOnlyList<AvailableDesignerReadModel>> GetDesignerWorkloadAsync(
        int page,
        int pageSize,
        int maxActiveProjects,
        string? search,
        string? capacityState,
        string sortBy,
        CancellationToken cancellationToken = default);
    Task<int> CountDesignerWorkloadAsync(
        int maxActiveProjects,
        string? search,
        string? capacityState,
        CancellationToken cancellationToken = default);
    Task<DesignerWorkloadSummaryReadModel> GetDesignerWorkloadSummaryAsync(
        int maxActiveProjects,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DesignerAssignedProjectReadModel>> GetDesignerAssignedProjectsAsync(
        Guid designerId,
        int page,
        int pageSize,
        string? bucket,
        CancellationToken cancellationToken = default);
    Task<int> CountDesignerAssignedProjectsAsync(
        Guid designerId,
        string? bucket,
        CancellationToken cancellationToken = default);
    Task<bool> IsActiveDesignerAsync(Guid designerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SalesWorkloadItemReadModel>> GetSalesWorkloadAsync(
        int page,
        int pageSize,
        int maxActiveProjects,
        string? search,
        string? capacityState,
        string? futurePressureState,
        string sortBy,
        CancellationToken cancellationToken = default);
    Task<int> CountSalesWorkloadAsync(
        int maxActiveProjects,
        string? search,
        string? capacityState,
        string? futurePressureState,
        CancellationToken cancellationToken = default);
    Task<SalesWorkloadSummaryReadModel> GetSalesWorkloadSummaryAsync(
        int maxActiveProjects,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SalesAssignedProjectReadModel>> GetSalesAssignedProjectsAsync(
        Guid salesId,
        int page,
        int pageSize,
        string? bucket,
        CancellationToken cancellationToken = default);
    Task<int> CountSalesAssignedProjectsAsync(
        Guid salesId,
        string? bucket,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UnassignedIntakeProjectReadModel>> GetUnassignedIntakeProjectsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<int> CountUnassignedIntakeProjectsAsync(CancellationToken cancellationToken = default);
    Task<bool> IsActiveSalesAsync(Guid salesId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Account>> GetPagedAsync(int page, int pageSize, string? search, string? status, bool includeDeleted, CancellationToken cancellationToken = default);
    Task<int> CountAsync(string? search, string? status, bool includeDeleted, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AccountFacetCountReadModel>> CountGroupedByStatusAsync(
        bool includeDeleted,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AccountFacetCountReadModel>> CountGroupedByRoleIdAsync(
        bool includeDeleted,
        CancellationToken cancellationToken = default);
}
