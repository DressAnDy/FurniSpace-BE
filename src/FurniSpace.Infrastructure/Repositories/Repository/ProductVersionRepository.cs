using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.Data;
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
}
