using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.Repositories.IRepository;

public interface ILayoutAssetRepository
{
    Task AddAsync(LayoutAsset layoutAsset, CancellationToken cancellationToken = default);

    Task<LayoutAsset?> GetByIdAsync(Guid layoutAssetId, CancellationToken cancellationToken = default);

    Task<LayoutAsset?> GetForUpdateAsync(Guid layoutAssetId, CancellationToken cancellationToken = default);

    Task<bool> AssetCodeExistsAsync(string normalizedAssetCode, CancellationToken cancellationToken = default);

    Task<bool> AssetCodeExistsExceptAsync(
        string normalizedAssetCode,
        Guid layoutAssetId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LayoutAsset>> GetPagedAsync(
        LayoutAssetType? assetType,
        LayoutAssetStatus? status,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(
        LayoutAssetType? assetType,
        LayoutAssetStatus? status,
        string? search,
        CancellationToken cancellationToken = default);
}
