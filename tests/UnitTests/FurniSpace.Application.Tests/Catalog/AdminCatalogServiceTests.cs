#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.DTOs.Catalog;
using FurniSpace.Application.Services.Catalog;
using FurniSpace.Application.Tests.TestDoubles;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.Products;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Xunit;

namespace FurniSpace.Application.Tests.Catalog;

public sealed class AdminCatalogServiceTests
{
    [Fact]
    public async Task GetProductsAsync_WithValidQuery_ReturnsMappedItems()
    {
        var productId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var catalog = new FakeCatalogRepository
        {
            AdminCatalogItems =
            [
                new AdminCatalogProductListItemReadModel
                {
                    ProductId = productId,
                    ProductCode = "PM-001",
                    ProductName = "Coffee Counter",
                    CategoryId = categoryId,
                    CategoryName = "Counter",
                    Status = ProductStatus.ACTIVE,
                    TotalVersionCount = 2,
                    ActiveVersionCount = 1,
                    DefaultVersionId = Guid.NewGuid(),
                    DefaultVersionCode = "PV-001",
                    DefaultVersionName = "Standard",
                    DefaultVersionStatus = ProductStatus.ACTIVE,
                    DefaultVersionEstimatedPrice = 1000m,
                    DefaultVersionDefaultTaxRate = 10m
                }
            ],
            AdminCatalogTotal = 1
        };
        var service = CreateService(catalog, categories: [CreateCategory(categoryId)]);

        var result = await service.GetProductsAsync(new AdminCatalogQueryDto
        {
            Page = 1,
            PageSize = 20
        });

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data.Items);
        Assert.Equal(productId, result.Data.Items[0].ProductId);
        Assert.Equal("Coffee Counter", result.Data.Items[0].ProductName);
        Assert.NotNull(result.Data.Items[0].DefaultVersionSummary);
        Assert.Equal("PV-001", result.Data.Items[0].DefaultVersionSummary!.VersionCode);
        Assert.Equal(10m, result.Data.Items[0].DefaultVersionSummary.DefaultTaxRate);
        Assert.Equal(1, result.Data.TotalCount);
    }

    [Fact]
    public async Task GetProductsAsync_WithInvalidPagination_ReturnsBadRequest()
    {
        var service = CreateService(new FakeCatalogRepository());

        var result = await service.GetProductsAsync(new AdminCatalogQueryDto
        {
            Page = 0,
            PageSize = 20
        });

        Assert.Equal(400, result.Status);
        Assert.Equal(CatalogErrorCodes.CatalogFilterInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task GetProductsAsync_WithInvalidSortField_ReturnsBadRequest()
    {
        var service = CreateService(new FakeCatalogRepository());

        var result = await service.GetProductsAsync(new AdminCatalogQueryDto
        {
            Page = 1,
            PageSize = 20,
            SortBy = "invalid"
        });

        Assert.Equal(400, result.Status);
        Assert.Equal(CatalogErrorCodes.CatalogSortInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task GetProductsAsync_WithUnknownCategory_ReturnsNotFound()
    {
        var service = CreateService(new FakeCatalogRepository());

        var result = await service.GetProductsAsync(new AdminCatalogQueryDto
        {
            Page = 1,
            PageSize = 20,
            CategoryId = Guid.NewGuid()
        });

        Assert.Equal(404, result.Status);
        Assert.Equal(CatalogErrorCodes.CategoryNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetProductsAsync_WithUnknownBusinessType_ReturnsNotFound()
    {
        var service = CreateService(new FakeCatalogRepository(), businessTypes: new StubBusinessTypeRepository());

        var result = await service.GetProductsAsync(new AdminCatalogQueryDto
        {
            Page = 1,
            PageSize = 20,
            BusinessTypeId = 99
        });

        Assert.Equal(404, result.Status);
        Assert.Equal(CatalogErrorCodes.BusinessTypeNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetProductsAsync_WithInvalidSortDirection_ReturnsBadRequest()
    {
        var service = CreateService(new FakeCatalogRepository());

        var result = await service.GetProductsAsync(new AdminCatalogQueryDto
        {
            Page = 1,
            PageSize = 20,
            SortDirection = "invalid"
        });

        Assert.Equal(400, result.Status);
        Assert.Equal(CatalogErrorCodes.CatalogSortInvalid, result.ErrorCode);
    }

    private static AdminCatalogService CreateService(
        ICatalogRepository catalog,
        IReadOnlyList<ProductCategoryReadModel>? categories = null,
        IBusinessTypeRepository? businessTypes = null)
    {
        var products = new StubProductRepository(categories ?? []);
        return new AdminCatalogService(
            catalog,
            products,
            businessTypes ?? new StubBusinessTypeRepository([1]));
    }

    private static ProductCategoryReadModel CreateCategory(Guid categoryId)
        => new() { CategoryId = categoryId, CategoryName = "Counter" };

    private sealed class StubProductRepository : IProductRepository
    {
        private readonly IReadOnlyList<ProductCategoryReadModel> _categories;

        public StubProductRepository(IReadOnlyList<ProductCategoryReadModel> categories)
        {
            _categories = categories;
        }

        public Task<ProductCategoryReadModel?> GetCategoryAsync(
            Guid categoryId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_categories.FirstOrDefault(category => category.CategoryId == categoryId));

        public Task<bool> ProductCodeExistsAsync(string productCode, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<ProductDetailReadModel?> GetDetailAsync(Guid productId, CancellationToken cancellationToken = default)
            => Task.FromResult<ProductDetailReadModel?>(null);

        public Task<IReadOnlyList<ProductListItemReadModel>> GetPublicListAsync(
            int page,
            int limit,
            IReadOnlyCollection<int>? businessTypeIds = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProductListItemReadModel>>([]);

        public Task<int> CountAsync(
            IReadOnlyCollection<int>? businessTypeIds = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<IReadOnlyList<ProductListItemReadModel>> GetPublicListByCategoryAsync(
            Guid categoryId,
            int page,
            int limit,
            bool includeDefaultVersion,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProductListItemReadModel>>([]);

        public Task<int> CountByCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<ProductListItemReadModel?> GetSearchIndexItemAsync(
            Guid productId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<ProductListItemReadModel?>(null);

        public Task<IReadOnlyList<ProductListItemReadModel>> GetSearchIndexPageAsync(
            int page,
            int limit,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProductListItemReadModel>>([]);

        public Task<ProductSearchResultReadModel> SearchPublicAsync(
            ProductSearchQueryReadModel query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ProductSearchResultReadModel());

        public Task<IReadOnlyList<ProductListItemReadModel>> SuggestPublicAsync(
            string query,
            int limit,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProductListItemReadModel>>([]);

        public Task<IReadOnlyList<ProductListItemReadModel>> GetSimilarPublicAsync(
            Guid productId,
            int limit,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProductListItemReadModel>>([]);

        public IQueryable<Product> Query() => Enumerable.Empty<Product>().AsQueryable();

        public Task<IReadOnlyList<Product>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Product>>([]);

        public Task AddAsync(Product product, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task AddRangeAsync(IEnumerable<Product> entities, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<Product?>(null);

        public void Update(Product entity)
        {
        }

        public void Remove(Product entity)
        {
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }

    private sealed class StubBusinessTypeRepository : IBusinessTypeRepository
    {
        private readonly HashSet<int> _ids;

        public StubBusinessTypeRepository(IReadOnlyCollection<int>? ids = null)
        {
            _ids = ids?.ToHashSet() ?? [];
        }

        public Task<BusinessType?> GetByIdAsync(int businessTypeId, CancellationToken cancellationToken = default)
        {
            if (!_ids.Contains(businessTypeId))
            {
                return Task.FromResult<BusinessType?>(null);
            }

            return Task.FromResult<BusinessType?>(new BusinessType
            {
                Id = businessTypeId,
                Code = $"TYPE_{businessTypeId}",
                Name = $"Type {businessTypeId}",
                Status = true
            });
        }

        public Task AddAsync(BusinessType businessType, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<BusinessType?> GetForUpdateAsync(int businessTypeId, CancellationToken cancellationToken = default)
            => GetByIdAsync(businessTypeId, cancellationToken);

        public Task<IReadOnlyList<BusinessType>> GetByIdsAsync(
            IReadOnlyCollection<int> businessTypeIds,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<BusinessType>>([]);

        public Task<bool> CodeExistsAsync(string normalizedCode, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<IReadOnlyList<BusinessType>> GetPagedAsync(
            bool status,
            string? keyword,
            int page,
            int limit,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<BusinessType>>([]);

        public Task<int> CountAsync(bool status, string? keyword, CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }
}
