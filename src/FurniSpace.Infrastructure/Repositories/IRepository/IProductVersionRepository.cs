using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.DTOs.ProductVersions;
using FurniSpace.Infrastructure.Repositories.Base;

namespace FurniSpace.Infrastructure.Repositories.IRepository;

public interface IProductVersionRepository : IGenericRepository<ProductVersion>
{
    Task<bool> VersionCodeExistsAsync(
        string versionCode,
        CancellationToken cancellationToken = default);

    Task<bool> ProductExistsAsync(
        Guid productId,
        CancellationToken cancellationToken = default);

    Task<ProductVersionDetailReadModel?> GetPublicDetailAsync(
        Guid productVersionId,
        CancellationToken cancellationToken = default);

    Task SetDefaultAsync(
        ProductVersion productVersion,
        CancellationToken cancellationToken = default);
}
