#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Infrastructure.ReadModels.Products;
using FurniSpace.Infrastructure.Repositories.IRepository;

namespace FurniSpace.Application.Tests.TestDoubles;

public sealed class FakeCatalogRepository : ICatalogRepository
{
    public Task<IReadOnlyList<AdminCatalogProductListItemReadModel>> GetAdminCatalogAsync(
        AdminCatalogQueryReadModel query,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<AdminCatalogProductListItemReadModel>>([]);

    public Task<int> CountAdminCatalogAsync(
        AdminCatalogQueryReadModel query,
        CancellationToken cancellationToken = default)
        => Task.FromResult(0);

    public Task<IReadOnlyList<ProductVersionManagementReadModel>> GetAdminVersionListAsync(
        ProductVersionListQueryReadModel query,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ProductVersionManagementReadModel>>([]);

    public Task<int> CountAdminVersionListAsync(
        ProductVersionListQueryReadModel query,
        CancellationToken cancellationToken = default)
        => Task.FromResult(0);

    public Task<IReadOnlyList<ProjectCatalogProductListItemReadModel>> GetProjectCatalogAsync(
        ProjectCatalogQueryReadModel query,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ProjectCatalogProductListItemReadModel>>([]);

    public Task<int> CountProjectCatalogAsync(
        ProjectCatalogQueryReadModel query,
        CancellationToken cancellationToken = default)
        => Task.FromResult(0);

    public Task<ProjectCatalogProductListItemReadModel?> GetProjectCatalogProductDetailAsync(
        Guid projectId,
        Guid productId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<ProjectCatalogProductListItemReadModel?>(null);

    public Task<ProjectCatalogEligibleVersionReadModel?> GetProjectEligibleVersionDetailAsync(
        Guid projectId,
        Guid productVersionId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<ProjectCatalogEligibleVersionReadModel?>(null);

    public int ActiveVersionCount { get; set; }

    public Task<int> CountActiveVersionsByProductAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(ActiveVersionCount);
}
