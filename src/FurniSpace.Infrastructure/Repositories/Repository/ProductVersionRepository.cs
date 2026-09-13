using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.ReadModels.Products;
using FurniSpace.Infrastructure.Repositories.Base;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Infrastructure.Repositories.Repository;

public sealed class ProductVersionRepository : GenericRepository<ProductVersion>, IProductVersionRepository
{
    public ProductVersionRepository(AppDbContext dbContext) : base(dbContext)
    {
    }

    public Task<bool> VersionCodeExistsAsync(
        string versionCode,
        CancellationToken cancellationToken = default)
    {
        return DbContext.ProductVersionSet.AnyAsync(
            version => version.VersionCode == versionCode,
            cancellationToken);
    }

    public Task<bool> ProductExistsAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.ProductSet.AnyAsync(product => product.ProductId == productId, cancellationToken);
    }

    public Task<ProductVersionDetailReadModel?> GetPublicDetailAsync(
        Guid productVersionId,
        CancellationToken cancellationToken = default)
    {
        return (
            from version in DbContext.ProductVersionSet
            join product in DbContext.ProductSet on version.ProductId equals product.ProductId
            where version.ProductVersionId == productVersionId &&
                  version.Status == ProductStatus.ACTIVE &&
                  version.IsPublic == true
            select new ProductVersionDetailReadModel
            {
                ProductVersionId = version.ProductVersionId,
                ProductId = version.ProductId,
                ProductName = product.ProductName,
                VersionCode = version.VersionCode,
                VersionName = version.VersionName,
                VersionType = version.VersionType,
                Material = version.Material,
                Color = version.Color,
                Width = version.Width,
                Height = version.Height,
                Depth = version.Depth,
                EstimatedPrice = version.EstimatedPrice,
                DimensionUnit = version.DimensionUnit,
                IsDefault = version.IsDefault,
                IsPublic = version.IsPublic,
                IsProjectSpecific = version.IsProjectSpecific,
                Status = version.Status
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProductVersionDetailReadModel>> GetValidDetailsAsync(
        IReadOnlyCollection<Guid> productVersionIds,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        if (productVersionIds.Count == 0)
        {
            return [];
        }

        return await (
            from version in DbContext.ProductVersionSet
            join product in DbContext.ProductSet on version.ProductId equals product.ProductId
            where productVersionIds.Contains(version.ProductVersionId) &&
                  product.Status == ProductStatus.ACTIVE &&
                  version.Status == ProductStatus.ACTIVE &&
                  (version.IsPublic == true ||
                   version.IsProjectSpecific == true && version.ProjectId == projectId)
            select new ProductVersionDetailReadModel
            {
                ProductVersionId = version.ProductVersionId,
                ProductId = version.ProductId,
                ProductName = product.ProductName,
                VersionCode = version.VersionCode,
                VersionName = version.VersionName,
                VersionType = version.VersionType,
                Material = version.Material,
                Color = version.Color,
                Width = version.Width,
                Height = version.Height,
                Depth = version.Depth,
                EstimatedPrice = version.EstimatedPrice,
                DimensionUnit = version.DimensionUnit,
                IsDefault = version.IsDefault,
                IsPublic = version.IsPublic,
                IsProjectSpecific = version.IsProjectSpecific,
                Status = version.Status
            })
            .ToListAsync(cancellationToken);
    }

    public async Task SetDefaultAsync(
        ProductVersion productVersion,
        CancellationToken cancellationToken = default)
    {
        var versions = await DbContext.ProductVersionSet
            .Where(version => version.ProductId == productVersion.ProductId)
            .ToListAsync(cancellationToken);

        foreach (var version in versions)
        {
            version.IsDefault = version.ProductVersionId == productVersion.ProductVersionId;
        }
    }

    public Task<int> CountProjectSpecificByProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.ProductVersionSet.CountAsync(
            version =>
                version.ProjectId == projectId &&
                version.VersionType == ProductVersionType.PROJECT_SPECIFIC,
            cancellationToken);
    }
}
