using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Infrastructure.Repositories.Repository;

public sealed class LayoutAssetRepository : ILayoutAssetRepository
{
    private readonly AppDbContext _dbContext;

    public LayoutAssetRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddAsync(LayoutAsset layoutAsset, CancellationToken cancellationToken = default)
    {
        return _dbContext.LayoutAssetSet.AddAsync(layoutAsset, cancellationToken).AsTask();
    }

    public Task<LayoutAsset?> GetByIdAsync(Guid layoutAssetId, CancellationToken cancellationToken = default)
    {
        return _dbContext.LayoutAssetSet
            .AsNoTracking()
            .FirstOrDefaultAsync(asset => asset.LayoutAssetId == layoutAssetId, cancellationToken);
    }

    public Task<LayoutAsset?> GetForUpdateAsync(Guid layoutAssetId, CancellationToken cancellationToken = default)
    {
        return _dbContext.LayoutAssetSet
            .FirstOrDefaultAsync(asset => asset.LayoutAssetId == layoutAssetId, cancellationToken);
    }

    public Task<bool> AssetCodeExistsAsync(string normalizedAssetCode, CancellationToken cancellationToken = default)
    {
        return _dbContext.LayoutAssetSet
            .AsNoTracking()
            .AnyAsync(asset => asset.AssetCode == normalizedAssetCode, cancellationToken);
    }

    public Task<bool> AssetCodeExistsExceptAsync(
        string normalizedAssetCode,
        Guid layoutAssetId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.LayoutAssetSet
            .AsNoTracking()
            .AnyAsync(
                asset => asset.AssetCode == normalizedAssetCode && asset.LayoutAssetId != layoutAssetId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<LayoutAsset>> GetPagedAsync(
        LayoutAssetType? assetType,
        LayoutAssetStatus? status,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        return await BuildQuery(assetType, status, search)
            .OrderBy(asset => asset.AssetName)
            .ThenBy(asset => asset.AssetCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAsync(
        LayoutAssetType? assetType,
        LayoutAssetStatus? status,
        string? search,
        CancellationToken cancellationToken = default)
    {
        return BuildQuery(assetType, status, search).CountAsync(cancellationToken);
    }

    private IQueryable<LayoutAsset> BuildQuery(
        LayoutAssetType? assetType,
        LayoutAssetStatus? status,
        string? search)
    {
        var query = _dbContext.LayoutAssetSet.AsNoTracking();

        if (assetType.HasValue)
        {
            query = query.Where(asset => asset.AssetType == assetType.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(asset => asset.Status == status.Value);
        }

        if (string.IsNullOrWhiteSpace(search))
        {
            return query;
        }

        var pattern = BuildSearchPattern(search);
        return query.Where(asset =>
            EF.Functions.ILike(asset.AssetCode, pattern, "\\") ||
            EF.Functions.ILike(asset.AssetName, pattern, "\\"));
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
