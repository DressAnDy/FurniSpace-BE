using FurniSpace.Infrastructure.ReadModels.Products;

namespace FurniSpace.Infrastructure.Repositories.IRepository;

public interface ICatalogRepository
{
    Task<IReadOnlyList<AdminCatalogProductListItemReadModel>> GetAdminCatalogAsync(
        AdminCatalogQueryReadModel query,
        CancellationToken cancellationToken = default);

    Task<int> CountAdminCatalogAsync(
        AdminCatalogQueryReadModel query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductVersionManagementReadModel>> GetAdminVersionListAsync(
        ProductVersionListQueryReadModel query,
        CancellationToken cancellationToken = default);

    Task<int> CountAdminVersionListAsync(
        ProductVersionListQueryReadModel query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectCatalogProductListItemReadModel>> GetProjectCatalogAsync(
        ProjectCatalogQueryReadModel query,
        CancellationToken cancellationToken = default);

    Task<int> CountProjectCatalogAsync(
        ProjectCatalogQueryReadModel query,
        CancellationToken cancellationToken = default);

    Task<ProjectCatalogProductListItemReadModel?> GetProjectCatalogProductDetailAsync(
        Guid projectId,
        Guid productId,
        CancellationToken cancellationToken = default);

    Task<ProjectCatalogEligibleVersionReadModel?> GetProjectEligibleVersionDetailAsync(
        Guid projectId,
        Guid productVersionId,
        CancellationToken cancellationToken = default);

    Task<int> CountActiveVersionsByProductAsync(
        Guid productId,
        CancellationToken cancellationToken = default);
}
