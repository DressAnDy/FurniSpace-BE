#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.DTOs.Products;
using FurniSpace.Application.Services.Products;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.DTOs.Products;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Xunit;

namespace FurniSpace.Application.Tests.Products;

public sealed class ProductServiceTests
{
    [Fact]
    public async Task CreateAsync_WithValidRequest_CreatesActiveProduct()
    {
        var categoryId = Guid.NewGuid();
        var repository = new FakeProductRepository(
            products: [],
            categories:
            [
                new ProductCategoryReadModel
                {
                    CategoryId = categoryId,
                    CategoryName = "Counter"
                }
            ]);
        var service = new ProductService(repository);

        var result = await service.CreateAsync(new CreateProductRequestDto
        {
            CategoryId = categoryId,
            ProductCode = " PM-COUNTER-001 ",
            ProductName = " Coffee Counter ",
            Description = " Counter template for cafe projects "
        });

        Assert.Equal(201, result.Status);
        Assert.Equal("Product master created successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.NotEqual(Guid.Empty, result.Data.ProductId);
        Assert.Equal(categoryId, result.Data.CategoryId);
        Assert.Equal("PM-COUNTER-001", result.Data.ProductCode);
        Assert.Equal("Coffee Counter", result.Data.ProductName);
        Assert.Equal("Counter template for cafe projects", result.Data.Description);
        Assert.Equal(ProductStatus.ACTIVE, result.Data.Status);
        Assert.Equal(1, repository.GetCategoryCallCount);
        Assert.Equal(1, repository.ProductCodeExistsCallCount);
        Assert.Equal(1, repository.AddCallCount);
        Assert.Equal(1, repository.SaveChangesCallCount);
        Assert.Single(repository.CreatedProducts);
    }

    [Fact]
    public async Task CreateAsync_WithOptionalFieldsBlank_UsesDefaultsAndNulls()
    {
        var categoryId = Guid.NewGuid();
        var repository = new FakeProductRepository(
            products: [],
            categories:
            [
                new ProductCategoryReadModel
                {
                    CategoryId = categoryId,
                    CategoryName = "Counter"
                }
            ]);
        var service = new ProductService(repository);

        var result = await service.CreateAsync(new CreateProductRequestDto
        {
            CategoryId = categoryId,
            ProductCode = " ",
            ProductName = "Coffee Counter",
            Description = " "
        });

        Assert.Equal(201, result.Status);
        Assert.NotNull(result.Data);
        Assert.Null(result.Data.ProductCode);
        Assert.Null(result.Data.Description);
        Assert.Equal(0, repository.ProductCodeExistsCallCount);
    }

    [Fact]
    public async Task CreateAsync_WithInvalidRequest_ReturnsValidationErrors()
    {
        var repository = new FakeProductRepository([]);
        var service = new ProductService(repository);

        var result = await service.CreateAsync(new CreateProductRequestDto
        {
            CategoryId = Guid.Empty,
            ProductCode = new string('P', 51),
            ProductName = " "
        });

        Assert.Equal(400, result.Status);
        Assert.Equal("Validation failed", result.Message);
        Assert.Contains("Category id is required.", result.Errors!);
        Assert.Contains("Product code must not exceed 50 characters.", result.Errors!);
        Assert.Contains("Product name is required.", result.Errors!);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetCategoryCallCount);
        Assert.Equal(0, repository.AddCallCount);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task CreateAsync_WithTooLongProductName_ReturnsValidationError()
    {
        var repository = new FakeProductRepository([]);
        var service = new ProductService(repository);

        var result = await service.CreateAsync(new CreateProductRequestDto
        {
            CategoryId = Guid.NewGuid(),
            ProductName = new string('N', 151)
        });

        Assert.Equal(400, result.Status);
        Assert.Contains("Product name must not exceed 150 characters.", result.Errors!);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetCategoryCallCount);
    }

    [Fact]
    public async Task CreateAsync_WithMissingCategory_ReturnsBadRequest()
    {
        var repository = new FakeProductRepository([]);
        var service = new ProductService(repository);

        var result = await service.CreateAsync(new CreateProductRequestDto
        {
            CategoryId = Guid.NewGuid(),
            ProductName = "Coffee Counter"
        });

        Assert.Equal(400, result.Status);
        Assert.Equal("Category does not exist.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(1, repository.GetCategoryCallCount);
        Assert.Equal(0, repository.ProductCodeExistsCallCount);
        Assert.Equal(0, repository.AddCallCount);
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateProductCode_ReturnsConflict()
    {
        var categoryId = Guid.NewGuid();
        var repository = new FakeProductRepository(
            products:
            [
                new ProductListItemReadModel
                {
                    ProductId = Guid.NewGuid(),
                    ProductCode = "PM-COUNTER-001",
                    ProductName = "Existing"
                }
            ],
            categories:
            [
                new ProductCategoryReadModel
                {
                    CategoryId = categoryId,
                    CategoryName = "Counter"
                }
            ]);
        var service = new ProductService(repository);

        var result = await service.CreateAsync(new CreateProductRequestDto
        {
            CategoryId = categoryId,
            ProductCode = "PM-COUNTER-001",
            ProductName = "Coffee Counter"
        });

        Assert.Equal(409, result.Status);
        Assert.Equal("Product code already exists.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(1, repository.GetCategoryCallCount);
        Assert.Equal(1, repository.ProductCodeExistsCallCount);
        Assert.Equal(0, repository.AddCallCount);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingProduct_ReturnsDetailWithVersionsAndDefaultVersion()
    {
        var productId = Guid.NewGuid();
        var defaultVersionId = Guid.NewGuid();
        var ignoredDefaultVersionId = Guid.NewGuid();
        var selectedDefaultVersion = new ProductVersionReadModel
        {
            ProductVersionId = defaultVersionId,
            VersionCode = "PV-COUNTER-001-V1",
            VersionName = "Coffee Counter - Standard Wood",
            VersionType = ProductVersionType.STANDARD,
            Material = "Plywood",
            Color = "Wood Brown",
            Width = 2000m,
            Height = 1050m,
            Depth = 650m,
            EstimatedPrice = 25000000m,
            IsDefault = false,
            IsPublic = true,
            IsProjectSpecific = false,
            Status = ProductStatus.ACTIVE,
            CreatedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)
        };
        var repository = new FakeProductRepository(
            products: [],
            details:
            [
                new ProductDetailReadModel
                {
                    ProductId = productId,
                    CategoryId = Guid.NewGuid(),
                    CategoryName = "Counter",
                    ProductCode = "PM-COUNTER-001",
                    ProductName = "Coffee Counter",
                    Description = "Counter template for cafe projects",
                    Status = ProductStatus.ACTIVE,
                    DefaultVersion = selectedDefaultVersion,
                    Versions =
                    [
                        new ProductVersionReadModel
                        {
                            ProductVersionId = ignoredDefaultVersionId,
                            VersionCode = "PRIVATE-DEFAULT",
                            VersionName = "Private Default",
                            IsDefault = true,
                            IsPublic = false,
                            Status = ProductStatus.ACTIVE,
                            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                        },
                        selectedDefaultVersion
                    ]
                }
            ]);
        var service = new ProductService(repository);

        var result = await service.GetByIdAsync(productId);

        Assert.Equal(200, result.Status);
        Assert.Equal(string.Empty, result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(productId, result.Data.ProductId);
        Assert.Equal("Counter", result.Data.CategoryName);
        Assert.Equal("PM-COUNTER-001", result.Data.ProductCode);
        Assert.Equal("Coffee Counter", result.Data.ProductName);
        Assert.Equal("Counter template for cafe projects", result.Data.Description);
        Assert.Equal(ProductStatus.ACTIVE, result.Data.Status);
        Assert.Equal(2, result.Data.Versions.Count);
        Assert.NotNull(result.Data.DefaultVersion);
        Assert.Equal(defaultVersionId, result.Data.DefaultVersion.ProductVersionId);
        Assert.Equal("PV-COUNTER-001-V1", result.Data.DefaultVersion.VersionCode);
        Assert.Equal("Coffee Counter - Standard Wood", result.Data.DefaultVersion.VersionName);
        Assert.Equal(ProductVersionType.STANDARD, result.Data.DefaultVersion.VersionType);
        Assert.Equal("Plywood", result.Data.DefaultVersion.Material);
        Assert.Equal("Wood Brown", result.Data.DefaultVersion.Color);
        Assert.Equal(2000m, result.Data.DefaultVersion.Width);
        Assert.Equal(1050m, result.Data.DefaultVersion.Height);
        Assert.Equal(650m, result.Data.DefaultVersion.Depth);
        Assert.Equal(25000000m, result.Data.DefaultVersion.EstimatedPrice);
        Assert.False(result.Data.DefaultVersion.IsDefault);
        Assert.True(result.Data.DefaultVersion.IsPublic);
        Assert.False(result.Data.DefaultVersion.IsProjectSpecific);
        Assert.Equal(ProductStatus.ACTIVE, result.Data.DefaultVersion.Status);
        Assert.Equal(1, repository.GetDetailCallCount);
    }

    [Fact]
    public async Task GetByIdAsync_WhenNoActivePublicVersionExists_ReturnsNullDefaultVersion()
    {
        var productId = Guid.NewGuid();
        var repository = new FakeProductRepository(
            products: [],
            details:
            [
                new ProductDetailReadModel
                {
                    ProductId = productId,
                    ProductName = "Coffee Counter",
                    Versions =
                    [
                        new ProductVersionReadModel
                        {
                            ProductVersionId = Guid.NewGuid(),
                            VersionCode = "INACTIVE",
                            VersionName = "Inactive",
                            IsPublic = true,
                            Status = ProductStatus.INACTIVE
                        },
                        new ProductVersionReadModel
                        {
                            ProductVersionId = Guid.NewGuid(),
                            VersionCode = "PRIVATE",
                            VersionName = "Private",
                            IsPublic = false,
                            Status = ProductStatus.ACTIVE
                        }
                    ]
                }
            ]);
        var service = new ProductService(repository);

        var result = await service.GetByIdAsync(productId);

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Null(result.Data.DefaultVersion);
        Assert.Equal(2, result.Data.Versions.Count);
    }

    [Fact]
    public async Task GetByIdAsync_WithEmptyProductId_ReturnsBadRequest()
    {
        var repository = new FakeProductRepository([]);
        var service = new ProductService(repository);

        var result = await service.GetByIdAsync(Guid.Empty);

        Assert.Equal(400, result.Status);
        Assert.Equal("Product id is required.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetDetailCallCount);
    }

    [Fact]
    public async Task GetByIdAsync_WithMissingProduct_ReturnsNotFound()
    {
        var repository = new FakeProductRepository([]);
        var service = new ProductService(repository);

        var result = await service.GetByIdAsync(Guid.NewGuid());

        Assert.Equal(404, result.Status);
        Assert.Equal("Product not found.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(1, repository.GetDetailCallCount);
    }

    [Fact]
    public async Task GetByCategoryAsync_WithValidRequest_ReturnsCategoryAndProducts()
    {
        var categoryId = Guid.NewGuid();
        var repository = new FakeProductRepository(
            products:
            [
                new ProductListItemReadModel
                {
                    ProductId = Guid.NewGuid(),
                    CategoryId = categoryId,
                    CategoryName = "Counter",
                    ProductCode = "PM-COUNTER-001",
                    ProductName = "Coffee Counter",
                    Status = ProductStatus.ACTIVE,
                    DefaultVersion = new ProductVersionReadModel
                    {
                        ProductVersionId = Guid.NewGuid(),
                        VersionCode = "PV-COUNTER-001-V1",
                        VersionName = "Coffee Counter - Standard Wood",
                        IsDefault = true,
                        IsPublic = true,
                        Status = ProductStatus.ACTIVE
                    }
                }
            ],
            categories:
            [
                new ProductCategoryReadModel
                {
                    CategoryId = categoryId,
                    CategoryName = "Counter"
                }
            ]);
        var service = new ProductService(repository);

        var result = await service.GetByCategoryAsync(categoryId, page: 1, limit: 20, includeDefaultVersion: true);

        Assert.Equal(200, result.Status);
        Assert.Equal(string.Empty, result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(categoryId, result.Data.Category.CategoryId);
        Assert.Equal("Counter", result.Data.Category.CategoryName);
        Assert.Equal(1, result.Data.Page);
        Assert.Equal(20, result.Data.Limit);
        Assert.Equal(1, result.Data.Total);
        var item = Assert.Single(result.Data.Items);
        Assert.Equal("Coffee Counter", item.ProductName);
        Assert.NotNull(item.DefaultVersion);
        Assert.Equal("PV-COUNTER-001-V1", item.DefaultVersion.VersionCode);
        Assert.Equal(1, repository.GetCategoryCallCount);
        Assert.Equal(1, repository.GetPublicListByCategoryCallCount);
        Assert.Equal(1, repository.CountByCategoryCallCount);
        Assert.True(repository.LastIncludeDefaultVersion);
    }

    [Fact]
    public async Task GetByCategoryAsync_WhenDefaultVersionIsExcluded_ReturnsProductsWithoutDefaultVersion()
    {
        var categoryId = Guid.NewGuid();
        var repository = new FakeProductRepository(
            products:
            [
                new ProductListItemReadModel
                {
                    ProductId = Guid.NewGuid(),
                    CategoryId = categoryId,
                    ProductName = "Coffee Counter",
                    DefaultVersion = new ProductVersionReadModel
                    {
                        ProductVersionId = Guid.NewGuid(),
                        VersionCode = "PV",
                        VersionName = "Version"
                    }
                }
            ],
            categories:
            [
                new ProductCategoryReadModel
                {
                    CategoryId = categoryId,
                    CategoryName = "Counter"
                }
            ]);
        var service = new ProductService(repository);

        var result = await service.GetByCategoryAsync(categoryId, page: 1, limit: 20, includeDefaultVersion: false);

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        var item = Assert.Single(result.Data.Items);
        Assert.Null(item.DefaultVersion);
        Assert.False(repository.LastIncludeDefaultVersion);
    }

    [Fact]
    public async Task GetByCategoryAsync_WithEmptyCategoryId_ReturnsBadRequest()
    {
        var repository = new FakeProductRepository([]);
        var service = new ProductService(repository);

        var result = await service.GetByCategoryAsync(Guid.Empty, page: 1, limit: 20, includeDefaultVersion: true);

        Assert.Equal(400, result.Status);
        Assert.Equal("Category id is required.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetCategoryCallCount);
        Assert.Equal(0, repository.GetPublicListByCategoryCallCount);
        Assert.Equal(0, repository.CountByCategoryCallCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetByCategoryAsync_WithInvalidPage_ReturnsBadRequest(int page)
    {
        var repository = new FakeProductRepository([]);
        var service = new ProductService(repository);

        var result = await service.GetByCategoryAsync(Guid.NewGuid(), page, limit: 20, includeDefaultVersion: true);

        Assert.Equal(400, result.Status);
        Assert.Equal("Page must be greater than zero.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetCategoryCallCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task GetByCategoryAsync_WithInvalidLimit_ReturnsBadRequest(int limit)
    {
        var repository = new FakeProductRepository([]);
        var service = new ProductService(repository);

        var result = await service.GetByCategoryAsync(Guid.NewGuid(), page: 1, limit, includeDefaultVersion: true);

        Assert.Equal(400, result.Status);
        Assert.Equal("Limit must be between 1 and 100.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetCategoryCallCount);
    }

    [Fact]
    public async Task GetByCategoryAsync_WithMissingCategory_ReturnsNotFound()
    {
        var repository = new FakeProductRepository([]);
        var service = new ProductService(repository);

        var result = await service.GetByCategoryAsync(Guid.NewGuid(), page: 1, limit: 20, includeDefaultVersion: true);

        Assert.Equal(404, result.Status);
        Assert.Equal("Category not found.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(1, repository.GetCategoryCallCount);
        Assert.Equal(0, repository.GetPublicListByCategoryCallCount);
        Assert.Equal(0, repository.CountByCategoryCallCount);
    }

    [Fact]
    public async Task GetAllAsync_WithValidPagination_ReturnsProductsWithDefaultVersion()
    {
        var productId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var repository = new FakeProductRepository(
        [
            new ProductListItemReadModel
            {
                ProductId = productId,
                CategoryId = Guid.NewGuid(),
                CategoryName = "Counter",
                ProductCode = "PM-COUNTER-001",
                ProductName = "Coffee Counter",
                Description = "Counter template for cafe projects",
                Status = ProductStatus.ACTIVE,
                DefaultVersion = new ProductVersionReadModel
                {
                    ProductVersionId = versionId,
                    VersionCode = "PV-COUNTER-001-V1",
                    VersionName = "Coffee Counter - Standard Wood",
                    VersionType = ProductVersionType.STANDARD,
                    Material = "Plywood",
                    Color = "Wood Brown",
                    Width = 2000m,
                    Height = 1050m,
                    Depth = 650m,
                    EstimatedPrice = 25000000m,
                    IsDefault = true,
                    IsPublic = true,
                    IsProjectSpecific = false,
                    Status = ProductStatus.ACTIVE
                }
            }
        ]);
        var service = new ProductService(repository);

        var result = await service.GetAllAsync(page: 1, limit: 20);

        Assert.Equal(200, result.Status);
        Assert.Equal(string.Empty, result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data.Page);
        Assert.Equal(20, result.Data.Limit);
        Assert.Equal(1, result.Data.Total);
        var item = Assert.Single(result.Data.Items);
        Assert.Equal(productId, item.ProductId);
        Assert.Equal("Counter", item.CategoryName);
        Assert.Equal("Coffee Counter", item.ProductName);
        Assert.NotNull(item.DefaultVersion);
        Assert.Equal(versionId, item.DefaultVersion.ProductVersionId);
        Assert.Equal("PV-COUNTER-001-V1", item.DefaultVersion.VersionCode);
        Assert.Equal("Coffee Counter - Standard Wood", item.DefaultVersion.VersionName);
        Assert.Equal(ProductVersionType.STANDARD, item.DefaultVersion.VersionType);
        Assert.Equal("Plywood", item.DefaultVersion.Material);
        Assert.Equal("Wood Brown", item.DefaultVersion.Color);
        Assert.Equal(2000m, item.DefaultVersion.Width);
        Assert.Equal(1050m, item.DefaultVersion.Height);
        Assert.Equal(650m, item.DefaultVersion.Depth);
        Assert.Equal(25000000m, item.DefaultVersion.EstimatedPrice);
        Assert.True(item.DefaultVersion.IsDefault);
        Assert.True(item.DefaultVersion.IsPublic);
        Assert.False(item.DefaultVersion.IsProjectSpecific);
        Assert.Equal(ProductStatus.ACTIVE, item.DefaultVersion.Status);
        Assert.Equal(1, repository.GetPublicListCallCount);
        Assert.Equal(1, repository.CountCallCount);
    }

    [Fact]
    public async Task GetAllAsync_WhenProductHasNoUsableVersion_ReturnsNullDefaultVersion()
    {
        var repository = new FakeProductRepository(
        [
            new ProductListItemReadModel
            {
                ProductId = Guid.NewGuid(),
                ProductName = "Coffee Counter",
                Status = ProductStatus.ACTIVE,
                DefaultVersion = null
            }
        ]);
        var service = new ProductService(repository);

        var result = await service.GetAllAsync(page: 1, limit: 20);

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        var item = Assert.Single(result.Data.Items);
        Assert.Null(item.DefaultVersion);
    }

    [Fact]
    public async Task GetAllAsync_WithSecondPage_ReturnsRequestedPage()
    {
        var repository = new FakeProductRepository(
        [
            new ProductListItemReadModel { ProductId = Guid.NewGuid(), ProductName = "A" },
            new ProductListItemReadModel { ProductId = Guid.NewGuid(), ProductName = "B" },
            new ProductListItemReadModel { ProductId = Guid.NewGuid(), ProductName = "C" }
        ]);
        var service = new ProductService(repository);

        var result = await service.GetAllAsync(page: 2, limit: 2);

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(3, result.Data.Total);
        var item = Assert.Single(result.Data.Items);
        Assert.Equal("C", item.ProductName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetAllAsync_WithInvalidPage_ReturnsBadRequest(int page)
    {
        var repository = new FakeProductRepository([]);
        var service = new ProductService(repository);

        var result = await service.GetAllAsync(page, limit: 20);

        Assert.Equal(400, result.Status);
        Assert.Equal("Page must be greater than zero.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetPublicListCallCount);
        Assert.Equal(0, repository.CountCallCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task GetAllAsync_WithInvalidLimit_ReturnsBadRequest(int limit)
    {
        var repository = new FakeProductRepository([]);
        var service = new ProductService(repository);

        var result = await service.GetAllAsync(page: 1, limit: limit);

        Assert.Equal(400, result.Status);
        Assert.Equal("Limit must be between 1 and 100.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetPublicListCallCount);
        Assert.Equal(0, repository.CountCallCount);
    }

    private sealed class FakeProductRepository : IProductRepository
    {
        private readonly IReadOnlyList<ProductListItemReadModel> _products;
        private readonly IReadOnlyList<ProductCategoryReadModel> _categories;
        private readonly IReadOnlyList<ProductDetailReadModel> _details;
        private readonly List<Product> _createdProducts = [];

        public FakeProductRepository(
            IReadOnlyList<ProductListItemReadModel> products,
            IReadOnlyList<ProductCategoryReadModel>? categories = null,
            IReadOnlyList<ProductDetailReadModel>? details = null)
        {
            _products = products;
            _categories = categories ?? [];
            _details = details ?? [];
        }

        public int GetDetailCallCount { get; private set; }
        public int ProductCodeExistsCallCount { get; private set; }
        public int AddCallCount { get; private set; }
        public int SaveChangesCallCount { get; private set; }
        public int GetPublicListCallCount { get; private set; }
        public int CountCallCount { get; private set; }
        public int GetCategoryCallCount { get; private set; }
        public int GetPublicListByCategoryCallCount { get; private set; }
        public int CountByCategoryCallCount { get; private set; }
        public bool LastIncludeDefaultVersion { get; private set; }
        public IReadOnlyList<Product> CreatedProducts => _createdProducts;

        public Task<bool> ProductCodeExistsAsync(
            string productCode,
            CancellationToken cancellationToken = default)
        {
            ProductCodeExistsCallCount++;
            return Task.FromResult(_products.Any(product =>
                string.Equals(product.ProductCode, productCode, StringComparison.Ordinal)));
        }

        public Task<ProductDetailReadModel?> GetDetailAsync(
            Guid productId,
            CancellationToken cancellationToken = default)
        {
            GetDetailCallCount++;
            return Task.FromResult(_details.FirstOrDefault(product => product.ProductId == productId));
        }

        public Task<IReadOnlyList<ProductListItemReadModel>> GetPublicListAsync(
            int page,
            int limit,
            CancellationToken cancellationToken = default)
        {
            GetPublicListCallCount++;
            return Task.FromResult<IReadOnlyList<ProductListItemReadModel>>(
                _products
                    .Skip((page - 1) * limit)
                    .Take(limit)
                    .ToList());
        }

        public Task<int> CountAsync(CancellationToken cancellationToken = default)
        {
            CountCallCount++;
            return Task.FromResult(_products.Count);
        }

        public Task<ProductCategoryReadModel?> GetCategoryAsync(
            Guid categoryId,
            CancellationToken cancellationToken = default)
        {
            GetCategoryCallCount++;
            return Task.FromResult(_categories.FirstOrDefault(category => category.CategoryId == categoryId));
        }

        public Task<IReadOnlyList<ProductListItemReadModel>> GetPublicListByCategoryAsync(
            Guid categoryId,
            int page,
            int limit,
            bool includeDefaultVersion,
            CancellationToken cancellationToken = default)
        {
            GetPublicListByCategoryCallCount++;
            LastIncludeDefaultVersion = includeDefaultVersion;
            return Task.FromResult<IReadOnlyList<ProductListItemReadModel>>(
                _products
                    .Where(product => product.CategoryId == categoryId)
                    .Skip((page - 1) * limit)
                    .Take(limit)
                    .Select(product => includeDefaultVersion
                        ? product
                        : new ProductListItemReadModel
                        {
                            ProductId = product.ProductId,
                            CategoryId = product.CategoryId,
                            CategoryName = product.CategoryName,
                            ProductCode = product.ProductCode,
                            ProductName = product.ProductName,
                            Description = product.Description,
                            Status = product.Status,
                            DefaultVersion = null
                        })
                    .ToList());
        }

        public Task<int> CountByCategoryAsync(
            Guid categoryId,
            CancellationToken cancellationToken = default)
        {
            CountByCategoryCallCount++;
            return Task.FromResult(_products.Count(product => product.CategoryId == categoryId));
        }

        public IQueryable<Product> Query() => Enumerable.Empty<Product>().AsQueryable();
        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Product?>(null);
        public Task<IReadOnlyList<Product>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Product>>([]);
        public Task AddAsync(Product entity, CancellationToken cancellationToken = default)
        {
            AddCallCount++;
            _createdProducts.Add(entity);
            return Task.CompletedTask;
        }

        public Task AddRangeAsync(IEnumerable<Product> entities, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Update(Product entity) { }
        public void Remove(Product entity) { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCallCount++;
            return Task.FromResult(1);
        }
    }
}
