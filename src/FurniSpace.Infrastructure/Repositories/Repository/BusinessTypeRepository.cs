using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Infrastructure.Repositories.Repository;

public sealed class BusinessTypeRepository : IBusinessTypeRepository
{
    private readonly AppDbContext _dbContext;

    public BusinessTypeRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<BusinessType?> GetByIdAsync(
        int businessTypeId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.BusinessTypeSet
            .AsNoTracking()
            .FirstOrDefaultAsync(businessType => businessType.Id == businessTypeId, cancellationToken);
    }

    public async Task<IReadOnlyList<BusinessType>> GetPagedAsync(
        bool status,
        string? keyword,
        int page,
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await BuildQuery(status, keyword)
            .OrderBy(businessType => businessType.Name)
            .ThenBy(businessType => businessType.Id)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAsync(
        bool status,
        string? keyword,
        CancellationToken cancellationToken = default)
    {
        return BuildQuery(status, keyword).CountAsync(cancellationToken);
    }

    private IQueryable<BusinessType> BuildQuery(bool status, string? keyword)
    {
        var query = _dbContext.BusinessTypeSet
            .AsNoTracking()
            .Where(businessType => businessType.Status == status);

        if (string.IsNullOrWhiteSpace(keyword))
        {
            return query;
        }

        var pattern = BuildSearchPattern(keyword);
        return query.Where(businessType =>
            EF.Functions.ILike(businessType.Code, pattern, "\\") ||
            EF.Functions.ILike(businessType.Name, pattern, "\\"));
    }

    private static string BuildSearchPattern(string keyword)
    {
        return $"%{EscapeLikePattern(keyword.Trim())}%";
    }

    private static string EscapeLikePattern(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
    }
}
