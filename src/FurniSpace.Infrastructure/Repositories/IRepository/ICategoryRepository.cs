using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.Repositories.Base;

namespace FurniSpace.Infrastructure.Repositories.IRepository;

public interface ICategoryRepository : IGenericRepository<Category>
{
    Task<bool> NameExistsAsync(
        string categoryName,
        CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(
        string categoryName,
        Guid excludedCategoryId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Category>> GetPagedAsync(
        int page,
        int limit,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);
}
