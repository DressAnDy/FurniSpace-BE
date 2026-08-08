using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.ReadModels.Products;
using FurniSpace.Infrastructure.Repositories.Base;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Infrastructure.Repositories.Repository;

public sealed class CatalogRepository : GenericRepository<Product>, ICatalogRepository
{
    private const string ProductReferenceType = "PRODUCT";
    private const string ProductVersionReferenceType = "PRODUCT_VERSION";

    public CatalogRepository(AppDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<IReadOnlyList<AdminCatalogProductListItemReadModel>> GetAdminCatalogAsync(
        AdminCatalogQueryReadModel query,
        CancellationToken cancellationToken = default)
    {
        var filtered = await LoadAdminCatalogFilteredAsync(query, cancellationToken);
        var sorted = ApplyAdminCatalogSort(filtered, query.SortBy, query.SortDirection);
        return sorted
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();
    }

    public async Task<int> CountAdminCatalogAsync(
        AdminCatalogQueryReadModel query,
        CancellationToken cancellationToken = default)
    {
        var filtered = await LoadAdminCatalogFilteredAsync(query, cancellationToken);
        return filtered.Count;
    }

    public async Task<IReadOnlyList<ProductVersionManagementReadModel>> GetAdminVersionListAsync(
        ProductVersionListQueryReadModel query,
        CancellationToken cancellationToken = default)
    {
        return await ApplyAdminVersionFilters(query)
            .OrderByDescending(version => version.IsDefault == true)
            .ThenByDescending(version => version.CreatedAt)
            .ThenBy(version => version.ProductVersionId)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAdminVersionListAsync(
        ProductVersionListQueryReadModel query,
        CancellationToken cancellationToken = default)
    {
        return ApplyAdminVersionFilters(query).CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProjectCatalogProductListItemReadModel>> GetProjectCatalogAsync(
        ProjectCatalogQueryReadModel query,
        CancellationToken cancellationToken = default)
    {
        var items = await BuildProjectCatalogItemsAsync(query, cancellationToken);
        return items
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();
    }

    public async Task<int> CountProjectCatalogAsync(
        ProjectCatalogQueryReadModel query,
        CancellationToken cancellationToken = default)
    {
        var items = await BuildProjectCatalogItemsAsync(query, cancellationToken);
        return items.Count;
    }

    public async Task<ProjectCatalogProductListItemReadModel?> GetProjectCatalogProductDetailAsync(
        Guid projectId,
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        var items = await BuildProjectCatalogItemsAsync(
            new ProjectCatalogQueryReadModel { ProjectId = projectId },
            cancellationToken);

        return items.FirstOrDefault(item => item.ProductId == productId);
    }

    public Task<ProjectCatalogEligibleVersionReadModel?> GetProjectEligibleVersionDetailAsync(
        Guid projectId,
        Guid productVersionId,
        CancellationToken cancellationToken = default)
    {
        return (
            from version in DbContext.ProductVersionSet
            join product in DbContext.ProductSet on version.ProductId equals product.ProductId
            where version.ProductVersionId == productVersionId &&
                  product.Status == ProductStatus.ACTIVE &&
                  version.Status == ProductStatus.ACTIVE &&
                  (version.IsPublic == true ||
                   version.IsProjectSpecific == true && version.ProjectId == projectId)
            select new ProjectCatalogEligibleVersionReadModel
            {
                ProductVersionId = version.ProductVersionId,
                ProductId = version.ProductId,
                ProjectId = version.ProjectId,
                VersionCode = version.VersionCode,
                VersionName = version.VersionName,
                VersionType = version.VersionType,
                Material = version.Material,
                Color = version.Color,
                Width = version.Width,
                Height = version.Height,
                Depth = version.Depth,
                DimensionUnit = version.DimensionUnit,
                EstimatedPrice = version.EstimatedPrice,
                IsProjectSpecific = version.IsProjectSpecific
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<int> CountActiveVersionsByProductAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.ProductVersionSet.CountAsync(
            version => version.ProductId == productId && version.Status == ProductStatus.ACTIVE,
            cancellationToken);
    }

    private async Task<List<AdminCatalogProductListItemReadModel>> LoadAdminCatalogFilteredAsync(
        AdminCatalogQueryReadModel filter,
        CancellationToken cancellationToken)
    {
        var items = await ApplyAdminCatalogSqlFilters(BuildAdminCatalogQuery(), filter)
            .ToListAsync(cancellationToken);

        if (!NeedsVersionMetadataFilter(filter))
        {
            return items;
        }

        return await FilterByVersionMetadataAsync(items, filter, cancellationToken);
    }

    private static bool NeedsVersionMetadataFilter(AdminCatalogQueryReadModel filter)
    {
        return filter.VersionStatus.HasValue ||
               filter.VersionType.HasValue ||
               filter.Has3DModel.HasValue;
    }

    private IQueryable<AdminCatalogProductListItemReadModel> BuildAdminCatalogQuery()
    {
        return
            from product in DbContext.ProductSet
            join category in DbContext.CategorySet
                on product.CategoryId equals category.CategoryId into categories
            from category in categories.DefaultIfEmpty()
            let versions = DbContext.ProductVersionSet.Where(version => version.ProductId == product.ProductId)
            let defaultVersion = versions
                .Where(version => version.IsDefault == true)
                .OrderBy(version => version.CreatedAt)
                .FirstOrDefault()
            select new AdminCatalogProductListItemReadModel
            {
                ProductId = product.ProductId,
                ProductCode = product.ProductCode,
                ProductName = product.ProductName,
                CategoryId = product.CategoryId,
                CategoryName = category == null ? null : category.CategoryName,
                BusinessTypeIds = product.BusinessTypeIds,
                Status = product.Status,
                TotalVersionCount = versions.Count(),
                ActiveVersionCount = versions.Count(version => version.Status == ProductStatus.ACTIVE),
                InactiveVersionCount = versions.Count(version => version.Status == ProductStatus.INACTIVE),
                ArchivedVersionCount = versions.Count(version => version.Status == ProductStatus.ARCHIVED),
                DefaultVersionId = defaultVersion == null ? null : defaultVersion.ProductVersionId,
                DefaultVersionCode = defaultVersion == null ? null : defaultVersion.VersionCode,
                DefaultVersionName = defaultVersion == null ? null : defaultVersion.VersionName,
                DefaultVersionStatus = defaultVersion == null ? null : defaultVersion.Status,
                DefaultVersionEstimatedPrice = defaultVersion == null ? null : defaultVersion.EstimatedPrice,
                DefaultVersionDefaultTaxRate = defaultVersion == null ? null : defaultVersion.DefaultTaxRate,
                CreatedAt = product.CreatedAt,
                UpdatedAt = product.UpdatedAt
            };
    }

    private static IQueryable<AdminCatalogProductListItemReadModel> ApplyAdminCatalogSqlFilters(
        IQueryable<AdminCatalogProductListItemReadModel> query,
        AdminCatalogQueryReadModel filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var keyword = filter.Keyword.Trim().ToLowerInvariant();
            query = query.Where(product =>
                product.ProductName.ToLower().Contains(keyword) ||
                (product.ProductCode != null && product.ProductCode.ToLower().Contains(keyword)));
        }

        if (filter.CategoryId.HasValue)
        {
            query = query.Where(product => product.CategoryId == filter.CategoryId.Value);
        }

        if (filter.BusinessTypeId.HasValue)
        {
            var businessTypeId = filter.BusinessTypeId.Value;
            query = query.Where(product =>
                product.BusinessTypeIds != null && product.BusinessTypeIds.Contains(businessTypeId));
        }

        if (filter.ProductStatus.HasValue)
        {
            query = query.Where(product => product.Status == filter.ProductStatus.Value);
        }

        if (filter.CreatedFrom.HasValue)
        {
            query = query.Where(product => product.CreatedAt >= filter.CreatedFrom.Value);
        }

        if (filter.CreatedTo.HasValue)
        {
            query = query.Where(product => product.CreatedAt <= filter.CreatedTo.Value);
        }

        if (filter.HasActiveVersion == true)
        {
            query = query.Where(product => product.ActiveVersionCount > 0);
        }
        else if (filter.HasActiveVersion == false)
        {
            query = query.Where(product => product.ActiveVersionCount == 0);
        }

        return query;
    }

    private async Task<List<AdminCatalogProductListItemReadModel>> FilterByVersionMetadataAsync(
        List<AdminCatalogProductListItemReadModel> items,
        AdminCatalogQueryReadModel filter,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return items;
        }

        var productIds = items.Select(item => item.ProductId).ToList();
        var versions = await DbContext.ProductVersionSet
            .Where(version => productIds.Contains(version.ProductId))
            .ToListAsync(cancellationToken);

        var model3DProductIds = filter.Has3DModel.HasValue
            ? await GetProductIdsWith3DModelAsync(productIds, cancellationToken)
            : null;

        return items
            .Where(item => MatchesVersionMetadata(item.ProductId, filter, versions, model3DProductIds))
            .ToList();
    }

    private static bool MatchesVersionMetadata(
        Guid productId,
        AdminCatalogQueryReadModel filter,
        List<ProductVersion> versions,
        HashSet<Guid>? model3DProductIds)
    {
        var productVersions = versions.Where(version => version.ProductId == productId).ToList();

        if (filter.VersionStatus.HasValue &&
            !productVersions.Any(version => version.Status == filter.VersionStatus.Value))
        {
            return false;
        }

        if (filter.VersionType.HasValue &&
            !productVersions.Any(version => version.VersionType == filter.VersionType.Value))
        {
            return false;
        }

        if (filter.Has3DModel == true && (model3DProductIds is null || !model3DProductIds.Contains(productId)))
        {
            return false;
        }

        if (filter.Has3DModel == false && model3DProductIds is not null && model3DProductIds.Contains(productId))
        {
            return false;
        }

        return true;
    }

    private async Task<HashSet<Guid>> GetProductIdsWith3DModelAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken)
    {
        var versionIds = await DbContext.ProductVersionSet
            .Where(version => productIds.Contains(version.ProductId))
            .Select(version => version.ProductVersionId)
            .ToListAsync(cancellationToken);

        var productRefs = await DbContext.FileLinkSet
            .Where(link =>
                link.FileType == FileType.MODEL_3D &&
                link.ReferenceType == ProductReferenceType &&
                productIds.Contains(link.ReferenceId))
            .Select(link => link.ReferenceId)
            .ToListAsync(cancellationToken);

        var versionRefs = await DbContext.FileLinkSet
            .Where(link =>
                link.FileType == FileType.MODEL_3D &&
                link.ReferenceType == ProductVersionReferenceType &&
                versionIds.Contains(link.ReferenceId))
            .Join(
                DbContext.ProductVersionSet,
                link => link.ReferenceId,
                version => version.ProductVersionId,
                (_, version) => version.ProductId)
            .ToListAsync(cancellationToken);

        return productRefs.Concat(versionRefs).ToHashSet();
    }

    private static List<AdminCatalogProductListItemReadModel> ApplyAdminCatalogSort(
        List<AdminCatalogProductListItemReadModel> items,
        string? sortBy,
        string? sortDirection)
    {
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        var normalizedSort = sortBy?.Trim().ToLowerInvariant();

        return normalizedSort switch
        {
            "productname" => descending
                ? items.OrderByDescending(item => item.ProductName).ToList()
                : items.OrderBy(item => item.ProductName).ToList(),
            "productcode" => descending
                ? items.OrderByDescending(item => item.ProductCode).ToList()
                : items.OrderBy(item => item.ProductCode).ToList(),
            "updatedat" => descending
                ? items.OrderByDescending(item => item.UpdatedAt).ToList()
                : items.OrderBy(item => item.UpdatedAt).ToList(),
            _ => descending
                ? items.OrderByDescending(item => item.CreatedAt).ToList()
                : items.OrderBy(item => item.CreatedAt).ToList()
        };
    }

    private IQueryable<ProductVersionManagementReadModel> ApplyAdminVersionFilters(ProductVersionListQueryReadModel query)
    {
        var versions = DbContext.ProductVersionSet.Where(version => version.ProductId == query.ProductId);

        if (query.Status.HasValue)
        {
            versions = versions.Where(version => version.Status == query.Status.Value);
        }

        if (query.VersionType.HasValue)
        {
            versions = versions.Where(version => version.VersionType == query.VersionType.Value);
        }

        if (query.IsDefault.HasValue)
        {
            versions = versions.Where(version => version.IsDefault == query.IsDefault.Value);
        }

        if (query.IsPublic.HasValue)
        {
            versions = versions.Where(version => version.IsPublic == query.IsPublic.Value);
        }

        if (query.IsProjectSpecific.HasValue)
        {
            versions = versions.Where(version => version.IsProjectSpecific == query.IsProjectSpecific.Value);
        }

        if (query.ProjectId.HasValue)
        {
            versions = versions.Where(version => version.ProjectId == query.ProjectId.Value);
        }

        return versions.Select(version => new ProductVersionManagementReadModel
        {
            ProductVersionId = version.ProductVersionId,
            ProductId = version.ProductId,
            VersionCode = version.VersionCode,
            VersionName = version.VersionName,
            VersionType = version.VersionType,
            Material = version.Material,
            Color = version.Color,
            Width = version.Width,
            Height = version.Height,
            Depth = version.Depth,
            EstimatedPrice = version.EstimatedPrice,
            DefaultTaxRate = version.DefaultTaxRate,
            IsDefault = version.IsDefault,
            IsPublic = version.IsPublic,
            IsProjectSpecific = version.IsProjectSpecific,
            Status = version.Status,
            DimensionUnit = version.DimensionUnit,
            ProjectId = version.ProjectId,
            CreatedAt = version.CreatedAt,
            UpdatedAt = version.UpdatedAt
        });
    }

    private async Task<List<ProjectCatalogProductListItemReadModel>> BuildProjectCatalogItemsAsync(
        ProjectCatalogQueryReadModel query,
        CancellationToken cancellationToken)
    {
        var eligibleVersions = await (
            from version in DbContext.ProductVersionSet
            join product in DbContext.ProductSet on version.ProductId equals product.ProductId
            join category in DbContext.CategorySet
                on product.CategoryId equals category.CategoryId into categories
            from category in categories.DefaultIfEmpty()
            where product.Status == ProductStatus.ACTIVE &&
                  version.Status == ProductStatus.ACTIVE &&
                  (version.IsPublic == true ||
                   version.IsProjectSpecific == true && version.ProjectId == query.ProjectId)
            select new
            {
                ProductId = product.ProductId,
                ProductCode = product.ProductCode,
                ProductName = product.ProductName,
                Description = product.Description,
                CategoryId = product.CategoryId,
                CategoryName = category == null ? null : category.CategoryName,
                BusinessTypeIds = product.BusinessTypeIds,
                Version = new ProjectCatalogEligibleVersionReadModel
                {
                    ProductVersionId = version.ProductVersionId,
                    ProductId = version.ProductId,
                    ProjectId = version.ProjectId,
                    VersionCode = version.VersionCode,
                    VersionName = version.VersionName,
                    VersionType = version.VersionType,
                    Material = version.Material,
                    Color = version.Color,
                    Width = version.Width,
                    Height = version.Height,
                    Depth = version.Depth,
                    DimensionUnit = version.DimensionUnit,
                    EstimatedPrice = version.EstimatedPrice,
                    IsProjectSpecific = version.IsProjectSpecific
                }
            })
            .ToListAsync(cancellationToken);

        var grouped = eligibleVersions
            .GroupBy(item => item.ProductId)
            .Select(group =>
            {
                var first = group.First();
                return new ProjectCatalogProductListItemReadModel
                {
                    ProductId = first.ProductId,
                    ProductCode = first.ProductCode,
                    ProductName = first.ProductName,
                    Description = first.Description,
                    CategoryId = first.CategoryId,
                    CategoryName = first.CategoryName,
                    BusinessTypeIds = first.BusinessTypeIds,
                    EligibleVersions = group.Select(item => item.Version).ToList()
                };
            })
            .ToList();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim().ToLowerInvariant();
            grouped = grouped
                .Where(item =>
                    item.ProductName.ToLower().Contains(keyword) ||
                    (item.ProductCode != null && item.ProductCode.ToLower().Contains(keyword)))
                .ToList();
        }

        if (query.CategoryId.HasValue)
        {
            grouped = grouped.Where(item => item.CategoryId == query.CategoryId.Value).ToList();
        }

        if (query.BusinessTypeId.HasValue)
        {
            var businessTypeId = query.BusinessTypeId.Value;
            grouped = grouped
                .Where(item => item.BusinessTypeIds != null && item.BusinessTypeIds.Contains(businessTypeId))
                .ToList();
        }

        if (query.VersionType.HasValue)
        {
            grouped = grouped
                .Select(item =>
                {
                    var filteredVersions = item.EligibleVersions
                        .Where(version => version.VersionType == query.VersionType.Value)
                        .ToList();
                    return new ProjectCatalogProductListItemReadModel
                    {
                        ProductId = item.ProductId,
                        ProductCode = item.ProductCode,
                        ProductName = item.ProductName,
                        Description = item.Description,
                        CategoryId = item.CategoryId,
                        CategoryName = item.CategoryName,
                        BusinessTypeIds = item.BusinessTypeIds,
                        EligibleVersions = filteredVersions
                    };
                })
                .Where(item => item.EligibleVersions.Count > 0)
                .ToList();
        }

        return grouped
            .OrderBy(item => item.ProductName)
            .ThenBy(item => item.ProductId)
            .ToList();
    }
}
