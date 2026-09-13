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
        var normalizedName = EscapeLikePattern(categoryName.Trim());
        return Query().AnyAsync(
            category => EF.Functions.ILike(category.CategoryName, normalizedName, "\\"),
            cancellationToken);
    }

    public Task<bool> NameExistsAsync(
        string categoryName,
        Guid excludedCategoryId,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = EscapeLikePattern(categoryName.Trim());
        return Query().AnyAsync(
            category =>
                category.CategoryId != excludedCategoryId &&
                EF.Functions.ILike(category.CategoryName, normalizedName, "\\"),
            cancellationToken);
    }

    private static string EscapeLikePattern(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
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
