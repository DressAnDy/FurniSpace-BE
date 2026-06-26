using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.ReadModels.Products;
using FurniSpace.Infrastructure.Repositories.Base;
using FurniSpace.Infrastructure.Common.Search;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace FurniSpace.Infrastructure.Repositories.Repository;

public sealed class ProductRepository : GenericRepository<Product>, IProductRepository
{
    private static readonly Expression<Func<ProductVersion, ProductVersionReadModel>> ProductVersionProjection =
        version => new ProductVersionReadModel
        {
            ProductVersionId = version.ProductVersionId,
            VersionCode = version.VersionCode,
            VersionName = version.VersionName,
            VersionType = version.VersionType,
            Material = version.Material,
            Color = version.Color,
            Width = version.Width,
            Height = version.Height,
            Depth = version.Depth,
            EstimatedPrice = version.EstimatedPrice,
            IsDefault = version.IsDefault,
            IsPublic = version.IsPublic,
            IsProjectSpecific = version.IsProjectSpecific,
            Status = version.Status,
            CreatedAt = version.CreatedAt
        };

    public ProductRepository(AppDbContext dbContext) : base(dbContext)
    {
    }

    public Task<bool> ProductCodeExistsAsync(
        string productCode,
        CancellationToken cancellationToken = default)
    {
        return Query().AnyAsync(
            product => product.ProductCode == productCode,
            cancellationToken);
    }

    public async Task<ProductDetailReadModel?> GetDetailAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        var product = await (
            from item in DbContext.ProductSet
            join category in DbContext.CategorySet
                on item.CategoryId equals category.CategoryId into categories
            from category in categories.DefaultIfEmpty()
            where item.ProductId == productId
            select new ProductDetailReadModel
            {
                ProductId = item.ProductId,
                CategoryId = item.CategoryId,
                CategoryName = category == null ? null : category.CategoryName,
                ProductCode = item.ProductCode,
                ProductName = item.ProductName,
                Description = item.Description,
                Status = item.Status
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (product is null)
        {
            return null;
        }

        var versions = await DbContext.ProductVersionSet
            .Where(version => version.ProductId == productId)
            .OrderByDescending(version => version.IsDefault == true)
            .ThenBy(version => version.CreatedAt)
            .ThenBy(version => version.ProductVersionId)
            .Select(ProductVersionProjection)
            .ToListAsync(cancellationToken);
        product.Versions = versions;
        product.DefaultVersion = versions
            .Where(IsUsablePublicVersion)
            .OrderByDescending(version => version.IsDefault == true)
            .ThenBy(version => version.CreatedAt)
            .ThenBy(version => version.ProductVersionId)
            .FirstOrDefault();

        return product;
    }

    public async Task<IReadOnlyList<ProductListItemReadModel>> GetPublicListAsync(
        int page,
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await BuildProductListQuery(categoryId: null, includeDefaultVersion: true)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        return Query().CountAsync(cancellationToken);
    }

    public Task<ProductCategoryReadModel?> GetCategoryAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.CategorySet
            .Where(category => category.CategoryId == categoryId)
            .Select(category => new ProductCategoryReadModel
            {
                CategoryId = category.CategoryId,
                CategoryName = category.CategoryName
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProductListItemReadModel>> GetPublicListByCategoryAsync(
        Guid categoryId,
        int page,
        int limit,
        bool includeDefaultVersion,
        CancellationToken cancellationToken = default)
    {
        return await BuildProductListQuery(categoryId, includeDefaultVersion)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    private IQueryable<ProductListItemReadModel> BuildProductListQuery(
        Guid? categoryId,
        bool includeDefaultVersion)
    {
        return
            from product in DbContext.ProductSet
            join category in DbContext.CategorySet
                on product.CategoryId equals category.CategoryId into categories
            from category in categories.DefaultIfEmpty()
            where !categoryId.HasValue || product.CategoryId == categoryId.Value
            orderby product.CreatedAt descending, product.ProductName
            select new ProductListItemReadModel
            {
                ProductId = product.ProductId,
                CategoryId = product.CategoryId,
                CategoryName = category == null ? null : category.CategoryName,
                ProductCode = product.ProductCode,
                ProductName = product.ProductName,
                Description = product.Description,
                Status = product.Status,
                DefaultVersion = includeDefaultVersion
                    ? DbContext.ProductVersionSet
                        .Where(version =>
                            version.ProductId == product.ProductId &&
                            version.Status == ProductStatus.ACTIVE &&
                            version.IsPublic == true)
                        .OrderByDescending(version => version.IsDefault == true)
                        .ThenBy(version => version.CreatedAt)
                        .ThenBy(version => version.ProductVersionId)
                        .Select(ProductVersionProjection)
                        .FirstOrDefault()
                    : null
            };
    }

    private static bool IsUsablePublicVersion(ProductVersionReadModel version)
    {
        return version.Status == ProductStatus.ACTIVE &&
            version.IsPublic == true;
    }

    public Task<int> CountByCategoryAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        return Query()
            .Where(product => product.CategoryId == categoryId)
            .CountAsync(cancellationToken);
    }

    public Task<ProductListItemReadModel?> GetSearchIndexItemAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        return BuildProductListQuery(categoryId: null, includeDefaultVersion: true)
            .Where(product => product.ProductId == productId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProductListItemReadModel>> GetSearchIndexPageAsync(
        int page,
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await BuildProductListQuery(categoryId: null, includeDefaultVersion: true)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<ProductSearchResultReadModel> SearchPublicAsync(
        ProductSearchQueryReadModel query,
        CancellationToken cancellationToken = default)
    {
        var candidates = await BuildProductListQuery(query.CategoryId, includeDefaultVersion: true)
            .ToListAsync(cancellationToken);

        var filtered = candidates
            .Where(ProductSearchDocumentMapper.IsIndexable)
            .Where(item => MatchesSearchQuery(item, query))
            .ToList();

        var sorted = ApplySearchSort(filtered, query.Sort);
        var total = sorted.Count;
        var items = sorted
            .Skip((query.Page - 1) * query.Limit)
            .Take(query.Limit)
            .ToList();

        return new ProductSearchResultReadModel
        {
            Items = items,
            Total = total
        };
    }

    public async Task<IReadOnlyList<ProductListItemReadModel>> SuggestPublicAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var candidates = await BuildProductListQuery(categoryId: null, includeDefaultVersion: true)
            .ToListAsync(cancellationToken);

        var normalizedQuery = query.Trim();
        return candidates
            .Where(ProductSearchDocumentMapper.IsIndexable)
            .Where(item => item.ProductName.StartsWith(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                ContainsIgnoreCase(item.ProductName, normalizedQuery))
            .OrderBy(item => item.ProductName, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToList();
    }

    public async Task<IReadOnlyList<ProductListItemReadModel>> GetSimilarPublicAsync(
        Guid productId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var source = await GetSearchIndexItemAsync(productId, cancellationToken);
        if (source is null)
        {
            return [];
        }

        var candidates = await BuildProductListQuery(categoryId: null, includeDefaultVersion: true)
            .ToListAsync(cancellationToken);

        return candidates
            .Where(ProductSearchDocumentMapper.IsIndexable)
            .Where(item => item.ProductId != productId)
            .Select(item => new { Item = item, Score = CalculateSimilarityScore(source, item) })
            .Where(entry => entry.Score > 0)
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.Item.ProductName, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .Select(entry => entry.Item)
            .ToList();
    }

    private static int CalculateSimilarityScore(
        ProductListItemReadModel source,
        ProductListItemReadModel candidate)
    {
        var score = 0;

        if (source.CategoryId.HasValue && source.CategoryId == candidate.CategoryId)
        {
            score += 3;
        }

        if (!string.IsNullOrWhiteSpace(source.DefaultVersion?.Material) &&
            string.Equals(
                source.DefaultVersion.Material,
                candidate.DefaultVersion?.Material,
                StringComparison.OrdinalIgnoreCase))
        {
            score += 2;
        }

        if (!string.IsNullOrWhiteSpace(source.DefaultVersion?.Color) &&
            string.Equals(
                source.DefaultVersion.Color,
                candidate.DefaultVersion?.Color,
                StringComparison.OrdinalIgnoreCase))
        {
            score += 1;
        }

        return score;
    }

    private static bool MatchesSearchQuery(ProductListItemReadModel item, ProductSearchQueryReadModel query)
    {
        if (!string.IsNullOrWhiteSpace(query.Material) &&
            !string.Equals(item.DefaultVersion?.Material, query.Material.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.Color) &&
            !string.Equals(item.DefaultVersion?.Color, query.Color.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var price = item.DefaultVersion?.EstimatedPrice;
        if (query.MinPrice.HasValue && (price is null || price < query.MinPrice.Value))
        {
            return false;
        }

        if (query.MaxPrice.HasValue && (price is null || price > query.MaxPrice.Value))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(query.Query))
        {
            return true;
        }

        var term = query.Query.Trim();
        return ContainsIgnoreCase(item.ProductName, term) ||
            ContainsIgnoreCase(item.Description, term) ||
            ContainsIgnoreCase(item.ProductCode, term) ||
            ContainsIgnoreCase(item.CategoryName, term) ||
            ContainsIgnoreCase(item.DefaultVersion?.Material, term) ||
            ContainsIgnoreCase(item.DefaultVersion?.Color, term);
    }

    private static bool ContainsIgnoreCase(string? value, string term)
    {
        return value is not null &&
            value.Contains(term, StringComparison.OrdinalIgnoreCase);
    }

    private static List<ProductListItemReadModel> ApplySearchSort(
        IReadOnlyList<ProductListItemReadModel> items,
        string? sort)
    {
        return sort?.Trim().ToLowerInvariant() switch
        {
            "price_asc" => items
                .OrderBy(item => item.DefaultVersion?.EstimatedPrice ?? decimal.MaxValue)
                .ThenBy(item => item.ProductName, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            "price_desc" => items
                .OrderByDescending(item => item.DefaultVersion?.EstimatedPrice ?? decimal.MinValue)
                .ThenBy(item => item.ProductName, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            "created_asc" => items
                .OrderBy(item => item.DefaultVersion?.CreatedAt ?? DateTime.MinValue)
                .ThenBy(item => item.ProductName, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            _ => items
                .OrderByDescending(item => item.DefaultVersion?.CreatedAt ?? DateTime.MinValue)
                .ThenBy(item => item.ProductName, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }
}
