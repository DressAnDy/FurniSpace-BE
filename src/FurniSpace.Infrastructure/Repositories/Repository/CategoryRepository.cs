using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.Repositories.Base;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Infrastructure.Repositories.Repository;

public sealed class CategoryRepository : GenericRepository<Category>, ICategoryRepository
{
    public CategoryRepository(AppDbContext dbContext) : base(dbContext)
    {
    }

    public Task<bool> NameExistsAsync(
        string categoryName,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = categoryName.Trim().ToLower();
        return Query().AnyAsync(
            category => category.CategoryName.ToLower() == normalizedName,
            cancellationToken);
    }

    public Task<bool> NameExistsAsync(
        string categoryName,
        Guid excludedCategoryId,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = categoryName.Trim().ToLower();
        return Query().AnyAsync(
            category =>
                category.CategoryId != excludedCategoryId &&
                category.CategoryName.ToLower() == normalizedName,
            cancellationToken);
    }

    public async Task<IReadOnlyList<Category>> GetPagedAsync(
        int page,
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await Query()
            .OrderBy(category => category.CategoryName)
            .ThenBy(category => category.CategoryId)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        return Query().CountAsync(cancellationToken);
    }
}
