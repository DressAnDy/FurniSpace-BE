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
    public IReadOnlyList<AdminCatalogProductListItemReadModel> AdminCatalogItems { get; set; } = [];
    public int AdminCatalogTotal { get; set; }
    public IReadOnlyList<ProductVersionManagementReadModel> AdminVersionItems { get; set; } = [];
    public int AdminVersionTotal { get; set; }
    public IReadOnlyList<ProjectCatalogProductListItemReadModel> ProjectCatalogItems { get; set; } = [];
    public int ProjectCatalogTotal { get; set; }
    public ProjectCatalogProductListItemReadModel? ProjectCatalogProductDetail { get; set; }
    public ProjectCatalogEligibleVersionReadModel? ProjectEligibleVersionDetail { get; set; }
    public int ActiveVersionCount { get; set; }

    public Task<IReadOnlyList<AdminCatalogProductListItemReadModel>> GetAdminCatalogAsync(
        AdminCatalogQueryReadModel query,
        CancellationToken cancellationToken = default)
        => Task.FromResult(AdminCatalogItems);

    public Task<int> CountAdminCatalogAsync(
        AdminCatalogQueryReadModel query,
        CancellationToken cancellationToken = default)
        => Task.FromResult(AdminCatalogTotal == 0 ? AdminCatalogItems.Count : AdminCatalogTotal);

    public Task<IReadOnlyList<ProductVersionManagementReadModel>> GetAdminVersionListAsync(
        ProductVersionListQueryReadModel query,
        CancellationToken cancellationToken = default)
        => Task.FromResult(AdminVersionItems);

    public Task<int> CountAdminVersionListAsync(
        ProductVersionListQueryReadModel query,
        CancellationToken cancellationToken = default)
        => Task.FromResult(AdminVersionTotal == 0 ? AdminVersionItems.Count : AdminVersionTotal);

    public Task<IReadOnlyList<ProjectCatalogProductListItemReadModel>> GetProjectCatalogAsync(
        ProjectCatalogQueryReadModel query,
        CancellationToken cancellationToken = default)
        => Task.FromResult(ProjectCatalogItems);

    public Task<int> CountProjectCatalogAsync(
        ProjectCatalogQueryReadModel query,
        CancellationToken cancellationToken = default)
        => Task.FromResult(ProjectCatalogTotal == 0 ? ProjectCatalogItems.Count : ProjectCatalogTotal);

    public Task<ProjectCatalogProductListItemReadModel?> GetProjectCatalogProductDetailAsync(
        Guid projectId,
        Guid productId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(ProjectCatalogProductDetail);

    public Task<ProjectCatalogEligibleVersionReadModel?> GetProjectEligibleVersionDetailAsync(
        Guid projectId,
        Guid productVersionId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(ProjectEligibleVersionDetail);

    public Task<int> CountActiveVersionsByProductAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(ActiveVersionCount);
}
