using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.ReadModels.Products;
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

    Task<IReadOnlyList<ProductVersionDetailReadModel>> GetValidDetailsAsync(
        IReadOnlyCollection<Guid> productVersionIds,
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task SetDefaultAsync(
        ProductVersion productVersion,
        CancellationToken cancellationToken = default);

    Task<int> CountProjectSpecificByProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, decimal?>> GetDefaultTaxRatesByIdsAsync(
        IReadOnlyCollection<Guid> productVersionIds,
        CancellationToken cancellationToken = default);
}
