using FurniSpace.Domain.Entities;

namespace FurniSpace.Infrastructure.Repositories.IRepository;

public interface IBusinessTypeRepository
{
    Task<BusinessType?> GetByIdAsync(
        int businessTypeId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BusinessType>> GetPagedAsync(
        bool status,
        string? keyword,
        int page,
        int limit,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(
        bool status,
        string? keyword,
        CancellationToken cancellationToken = default);
}
