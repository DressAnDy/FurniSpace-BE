using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.Repositories.Base;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Infrastructure.Repositories.Repository;

public sealed class ProductRepository : GenericRepository<Product>, IProductRepository
{
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
                ProductType = item.ProductType,
                Status = item.Status
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (product is null)
        {
            return null;
        }

        product.Versions = await DbContext.ProductVersionSet
            .Where(version => version.ProductId == productId)
            .OrderByDescending(version => version.IsDefault == true)
            .ThenBy(version => version.CreatedAt)
            .ThenBy(version => version.ProductVersionId)
            .Select(version => new ProductVersionReadModel
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
            })
            .ToListAsync(cancellationToken);

        return product;
    }

    public async Task<IReadOnlyList<ProductListItemReadModel>> GetPublicListAsync(
        int page,
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await (
            from product in DbContext.ProductSet
            join category in DbContext.CategorySet
                on product.CategoryId equals category.CategoryId into categories
            from category in categories.DefaultIfEmpty()
            orderby product.CreatedAt descending, product.ProductName
            select new ProductListItemReadModel
            {
                ProductId = product.ProductId,
                CategoryId = product.CategoryId,
                CategoryName = category == null ? null : category.CategoryName,
                ProductCode = product.ProductCode,
                ProductName = product.ProductName,
                Description = product.Description,
                ProductType = product.ProductType,
                Status = product.Status,
                DefaultVersion = DbContext.ProductVersionSet
                    .Where(version =>
                        version.ProductId == product.ProductId &&
                        version.Status == "ACTIVE" &&
                        version.IsPublic == true)
                    .OrderByDescending(version => version.IsDefault == true)
                    .ThenBy(version => version.CreatedAt)
                    .ThenBy(version => version.ProductVersionId)
                    .Select(version => new ProductVersionReadModel
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
                    })
                    .FirstOrDefault()
            })
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
        return await (
            from product in DbContext.ProductSet
            join category in DbContext.CategorySet
                on product.CategoryId equals category.CategoryId into categories
            from category in categories.DefaultIfEmpty()
            where product.CategoryId == categoryId
            orderby product.CreatedAt descending, product.ProductName
            select new ProductListItemReadModel
            {
                ProductId = product.ProductId,
                CategoryId = product.CategoryId,
                CategoryName = category == null ? null : category.CategoryName,
                ProductCode = product.ProductCode,
                ProductName = product.ProductName,
                Description = product.Description,
                ProductType = product.ProductType,
                Status = product.Status,
                DefaultVersion = includeDefaultVersion
                    ? DbContext.ProductVersionSet
                        .Where(version =>
                            version.ProductId == product.ProductId &&
                            version.Status == "ACTIVE" &&
                            version.IsPublic == true)
                        .OrderByDescending(version => version.IsDefault == true)
                        .ThenBy(version => version.CreatedAt)
                        .ThenBy(version => version.ProductVersionId)
                        .Select(version => new ProductVersionReadModel
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
                        })
                        .FirstOrDefault()
                    : null
            })
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountByCategoryAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        return Query()
            .Where(product => product.CategoryId == categoryId)
            .CountAsync(cancellationToken);
    }
}
