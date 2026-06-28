#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.DTOs.Products;
using FurniSpace.Application.Services.Products;
using FurniSpace.Application.Tests.TestDoubles;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Search;
using FurniSpace.Infrastructure.ReadModels.Products;
using FurniSpace.Infrastructure.Interfaces;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Xunit;

namespace FurniSpace.Application.Tests.Products;

public sealed class ProductServiceTests
{
    [Fact]
    public async Task UpdateAsync_WithValidRequest_UpdatesProduct()
    {
        var productId = Guid.NewGuid();
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
            ],
            entities:
            [
                new Product
                {
                    ProductId = productId,
                    CategoryId = Guid.NewGuid(),
                    ProductCode = "PM-COUNTER-001",
                    ProductName = "Coffee Counter",
                    Description = "Old description",
                    Status = ProductStatus.ACTIVE
                }
            ]);
        var service = CatalogServiceTestHelper.CreateProductService(repository, new FakeCatalogProjectFileRepository());

        var result = await service.UpdateAsync(productId, new UpdateProductRequestDto
        {
            CategoryId = categoryId,
            ProductName = " Coffee Counter Updated ",
            Description = " Updated counter template for cafe projects "
        });

        Assert.Equal(200, result.Status);
        Assert.Equal("Product master updated successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(productId, result.Data.ProductId);
        Assert.Equal(categoryId, result.Data.CategoryId);
        Assert.Equal("PM-COUNTER-001", result.Data.ProductCode);
        Assert.Equal("Coffee Counter Updated", result.Data.ProductName);
        Assert.Equal("Updated counter template for cafe projects", result.Data.Description);
        Assert.Equal(ProductStatus.ACTIVE, result.Data.Status);
        Assert.Equal(1, repository.GetByIdCallCount);
        Assert.Equal(1, repository.GetCategoryCallCount);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateAsync_WithBlankDescription_ReturnsNullDescription()
    {
        var productId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var repository = new FakeProductRepository(
            products: [],
            categories: [new ProductCategoryReadModel { CategoryId = categoryId, CategoryName = "Counter" }],
            entities:
            [
                new Product
                {
                    ProductId = productId,
                    CategoryId = categoryId,
                    ProductName = "Coffee Counter",
                    Status = null
                }
            ]);
        var service = CatalogServiceTestHelper.CreateProductService(repository, new FakeCatalogProjectFileRepository());

        var result = await service.UpdateAsync(productId, new UpdateProductRequestDto
        {
            CategoryId = categoryId,
            ProductName = "Coffee Counter Updated",
            Description = " "
        });

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Null(result.Data.Description);
        Assert.Equal(ProductStatus.ACTIVE, result.Data.Status);
    }

    [Fact]
    public async Task UpdateAsync_WithEmptyProductId_ReturnsBadRequest()
    {
        var repository = new FakeProductRepository([]);
        var service = CatalogServiceTestHelper.CreateProductService(repository, new FakeCatalogProjectFileRepository());

        var result = await service.UpdateAsync(Guid.Empty, new UpdateProductRequestDto
        {
            CategoryId = Guid.NewGuid(),
            ProductName = "Coffee Counter"
        });

        Assert.Equal(400, result.Status);
        Assert.Equal("Product id is required.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetByIdCallCount);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateAsync_WithInvalidRequest_ReturnsValidationErrors()
    {
        var repository = new FakeProductRepository([]);
        var service = CatalogServiceTestHelper.CreateProductService(repository, new FakeCatalogProjectFileRepository());

        var result = await service.UpdateAsync(Guid.NewGuid(), new UpdateProductRequestDto
        {
            CategoryId = Guid.Empty,
            ProductName = " "
        });

        Assert.Equal(400, result.Status);
        Assert.Equal("Validation failed", result.Message);
        Assert.Contains("Category id is required.", result.Errors!);
        Assert.Contains("Product name is required.", result.Errors!);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetByIdCallCount);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateAsync_WithTooLongProductName_ReturnsValidationError()
    {
        var repository = new FakeProductRepository([]);
        var service = CatalogServiceTestHelper.CreateProductService(repository, new FakeCatalogProjectFileRepository());

        var result = await service.UpdateAsync(Guid.NewGuid(), new UpdateProductRequestDto
        {
            CategoryId = Guid.NewGuid(),
            ProductName = new string('N', 151)
        });

        Assert.Equal(400, result.Status);
        Assert.Contains("Product name must not exceed 150 characters.", result.Errors!);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetByIdCallCount);
    }

    [Fact]
    public async Task UpdateAsync_WithMissingProduct_ReturnsNotFound()
    {
        var repository = new FakeProductRepository([]);
        var service = CatalogServiceTestHelper.CreateProductService(repository, new FakeCatalogProjectFileRepository());

        var result = await service.UpdateAsync(Guid.NewGuid(), new UpdateProductRequestDto
        {
            CategoryId = Guid.NewGuid(),
            ProductName = "Coffee Counter"
        });

        Assert.Equal(404, result.Status);
        Assert.Equal("Product not found.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(1, repository.GetByIdCallCount);
        Assert.Equal(0, repository.GetCategoryCallCount);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateAsync_WithMissingCategory_ReturnsBadRequest()
    {
        var productId = Guid.NewGuid();
        var repository = new FakeProductRepository(
            products: [],
            entities:
            [
                new Product
                {
                    ProductId = productId,
                    ProductName = "Coffee Counter"
                }
            ]);
        var service = CatalogServiceTestHelper.CreateProductService(repository, new FakeCatalogProjectFileRepository());

        var result = await service.UpdateAsync(productId, new UpdateProductRequestDto
        {
            CategoryId = Guid.NewGuid(),
            ProductName = "Coffee Counter Updated"
        });

        Assert.Equal(400, result.Status);
        Assert.Equal("Category does not exist.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(1, repository.GetByIdCallCount);
        Assert.Equal(1, repository.GetCategoryCallCount);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

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
        var service = CatalogServiceTestHelper.CreateProductService(repository, new FakeCatalogProjectFileRepository());

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
        var service = CatalogServiceTestHelper.CreateProductService(repository, new FakeCatalogProjectFileRepository());

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
        var service = CatalogServiceTestHelper.CreateProductService(repository, new FakeCatalogProjectFileRepository());

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
        var service = CatalogServiceTestHelper.CreateProductService(repository, new FakeCatalogProjectFileRepository());

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
        var service = CatalogServiceTestHelper.CreateProductService(repository, new FakeCatalogProjectFileRepository());

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
        var service = CatalogServiceTestHelper.CreateProductService(repository, new FakeCatalogProjectFileRepository());

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
        var service = CatalogServiceTestHelper.CreateProductService(repository, new FakeCatalogProjectFileRepository());

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
        var service = CatalogServiceTestHelper.CreateProductService(repository, new FakeCatalogProjectFileRepository());

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
        var service = CatalogServiceTestHelper.CreateProductService(repository, new FakeCatalogProjectFileRepository());

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
        var service = CatalogServiceTestHelper.CreateProductService(repository, new FakeCatalogProjectFileRepository());

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
        var service = CatalogServiceTestHelper.CreateProductService(repository, new FakeCatalogProjectFileRepository());

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
        var service = CatalogServiceTestHelper.CreateProductService(repository, new FakeCatalogProjectFileRepository());

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
        var service = CatalogServiceTestHelper.CreateProductService(repository, new FakeCatalogProjectFileRepository());

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
        var service = CatalogServiceTestHelper.CreateProductService(repository, new FakeCatalogProjectFileRepository());

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
        var service = CatalogServiceTestHelper.CreateProductService(repository, new FakeCatalogProjectFileRepository());

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
        var service = CatalogServiceTestHelper.CreateProductService(repository, new FakeCatalogProjectFileRepository());

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
        var service = CatalogServiceTestHelper.CreateProductService(repository, new FakeCatalogProjectFileRepository());

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
        var service = CatalogServiceTestHelper.CreateProductService(repository, new FakeCatalogProjectFileRepository());

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
        var service = CatalogServiceTestHelper.CreateProductService(repository, new FakeCatalogProjectFileRepository());

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
        var service = CatalogServiceTestHelper.CreateProductService(repository, new FakeCatalogProjectFileRepository());

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
        var service = CatalogServiceTestHelper.CreateProductService(repository, new FakeCatalogProjectFileRepository());

        var result = await service.GetAllAsync(page: 1, limit: limit);

        Assert.Equal(400, result.Status);
        Assert.Equal("Limit must be between 1 and 100.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetPublicListCallCount);
        Assert.Equal(0, repository.CountCallCount);
    }

    [Fact]
    public async Task GetAllAsync_WithProductPreviewFile_ReturnsThumbnail()
    {
        var productId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var fileLinkId = Guid.NewGuid();
        var repository = new FakeProductRepository(
        [
            new ProductListItemReadModel
            {
                ProductId = productId,
                ProductName = "Modern Floor Lamp",
                Status = ProductStatus.ACTIVE
            }
        ]);
        var files = new FakeCatalogProjectFileRepository
        {
            CatalogFiles =
            [
                CreateCatalogFile(productId, "PRODUCT", fileId, fileLinkId, FileType.PRODUCT_PREVIEW, "lamp-preview.jpg")
            ]
        };
        var service = CatalogServiceTestHelper.CreateProductService(repository, files);

        var result = await service.GetAllAsync(page: 1, limit: 20);

        Assert.Equal(200, result.Status);
        var item = Assert.Single(result.Data!.Items);
        Assert.NotNull(item.Thumbnail);
        Assert.Equal(fileId, item.Thumbnail.FileId);
        Assert.Equal(FileType.PRODUCT_PREVIEW, item.Thumbnail.FileType);
    }

    [Fact]
    public async Task GetByIdAsync_WithProductAndVersionFiles_ReturnsFilesOnDetail()
    {
        var productId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var repository = new FakeProductRepository(
            products: [],
            details:
            [
                new ProductDetailReadModel
                {
                    ProductId = productId,
                    ProductName = "Modern Floor Lamp",
                    Status = ProductStatus.ACTIVE,
                    Versions =
                    [
                        new ProductVersionReadModel
                        {
                            ProductVersionId = versionId,
                            VersionCode = "LAMP-001-STD",
                            VersionName = "Standard White",
                            Status = ProductStatus.ACTIVE,
                            IsPublic = true
                        }
                    ],
                    DefaultVersion = new ProductVersionReadModel
                    {
                        ProductVersionId = versionId,
                        VersionCode = "LAMP-001-STD",
                        VersionName = "Standard White",
                        Status = ProductStatus.ACTIVE,
                        IsPublic = true
                    }
                }
            ]);
        var files = new FakeCatalogProjectFileRepository
        {
            CatalogFiles =
            [
                CreateCatalogFile(productId, "PRODUCT", Guid.NewGuid(), Guid.NewGuid(), FileType.PRODUCT_PREVIEW, "product-preview.jpg"),
                CreateCatalogFile(versionId, "PRODUCT_VERSION", Guid.NewGuid(), Guid.NewGuid(), FileType.MODEL_3D, "lamp.glb", "model/gltf-binary")
            ]
        };
        var service = CatalogServiceTestHelper.CreateProductService(repository, files);

        var result = await service.GetByIdAsync(productId);

        Assert.Equal(200, result.Status);
        Assert.Single(result.Data!.Files);
        Assert.Equal(FileType.PRODUCT_PREVIEW, result.Data.Files[0].FileType);
        Assert.Single(result.Data.DefaultVersion!.Files);
        Assert.Equal(FileType.MODEL_3D, result.Data.DefaultVersion.Files[0].FileType);
    }

    [Fact]
    public async Task GetByCategoryAsync_WithThumbnailFiles_ReturnsThumbnailOnItems()
    {
        var categoryId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var repository = new FakeProductRepository(
        [
            new ProductListItemReadModel
            {
                ProductId = productId,
                CategoryId = categoryId,
                ProductName = "Modern Floor Lamp",
                Status = ProductStatus.ACTIVE
            }
        ],
        categories:
        [
            new ProductCategoryReadModel
            {
                CategoryId = categoryId,
                CategoryName = "Lighting"
            }
        ]);
        var files = new FakeCatalogProjectFileRepository
        {
            CatalogFiles =
            [
                CreateCatalogFile(productId, "PRODUCT", Guid.NewGuid(), Guid.NewGuid(), FileType.PRODUCT_PREVIEW, "lamp-preview.jpg")
            ]
        };
        var service = CatalogServiceTestHelper.CreateProductService(repository, files);

        var result = await service.GetByCategoryAsync(categoryId, page: 1, limit: 20, includeDefaultVersion: true);

        Assert.Equal(200, result.Status);
        Assert.NotNull(Assert.Single(result.Data!.Items).Thumbnail);
    }

    [Fact]
    public async Task GetByIdAsync_WithOrderedPreviewFiles_ReturnsFilesInDisplayOrder()
    {
        var productId = Guid.NewGuid();
        var fileId1 = Guid.NewGuid();
        var fileId2 = Guid.NewGuid();
        var fileId3 = Guid.NewGuid();
        var repository = new FakeProductRepository(
            products: [],
            details:
            [
                new ProductDetailReadModel
                {
                    ProductId = productId,
                    ProductName = "Modern Floor Lamp",
                    Status = ProductStatus.ACTIVE
                }
            ]);
        var baseTime = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var files = new FakeCatalogProjectFileRepository
        {
            CatalogFiles =
            [
                CreateCatalogFile(productId, "PRODUCT", fileId3, Guid.NewGuid(), FileType.PRODUCT_PREVIEW, "third.jpg", displayOrder: 3, uploadedAt: baseTime),
                CreateCatalogFile(productId, "PRODUCT", fileId1, Guid.NewGuid(), FileType.PRODUCT_PREVIEW, "first.jpg", displayOrder: 1, uploadedAt: baseTime.AddMinutes(-10)),
                CreateCatalogFile(productId, "PRODUCT", fileId2, Guid.NewGuid(), FileType.PRODUCT_PREVIEW, "second.jpg", displayOrder: 2, uploadedAt: baseTime.AddMinutes(-5))
            ]
        };
        var service = CatalogServiceTestHelper.CreateProductService(repository, files);

        var result = await service.GetByIdAsync(productId);

        Assert.Equal(200, result.Status);
        Assert.Equal(3, result.Data!.Files.Count);
        Assert.Equal(fileId1, result.Data.Files[0].FileId);
        Assert.Equal(1, result.Data.Files[0].DisplayOrder);
        Assert.Equal(fileId2, result.Data.Files[1].FileId);
        Assert.Equal(fileId3, result.Data.Files[2].FileId);
    }

    [Fact]
    public async Task GetAllAsync_WithPrimaryPreview_UsesPrimaryAsThumbnail()
    {
        var productId = Guid.NewGuid();
        var primaryFileId = Guid.NewGuid();
        var firstOrderFileId = Guid.NewGuid();
        var repository = new FakeProductRepository(
        [
            new ProductListItemReadModel
            {
                ProductId = productId,
                ProductName = "Modern Floor Lamp",
                Status = ProductStatus.ACTIVE
            }
        ]);
        var baseTime = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var files = new FakeCatalogProjectFileRepository
        {
            CatalogFiles =
            [
                CreateCatalogFile(productId, "PRODUCT", firstOrderFileId, Guid.NewGuid(), FileType.PRODUCT_PREVIEW, "first.jpg", displayOrder: 1, uploadedAt: baseTime),
                CreateCatalogFile(productId, "PRODUCT", primaryFileId, Guid.NewGuid(), FileType.PRODUCT_PREVIEW, "primary.jpg", displayOrder: 2, isPrimary: true, uploadedAt: baseTime.AddMinutes(-5))
            ]
        };
        var service = CatalogServiceTestHelper.CreateProductService(repository, files);

        var result = await service.GetAllAsync(page: 1, limit: 20);

        Assert.Equal(200, result.Status);
        var item = Assert.Single(result.Data!.Items);
        Assert.NotNull(item.Thumbnail);
        Assert.Equal(primaryFileId, item.Thumbnail.FileId);
    }

    [Fact]
    public async Task GetByIdAsync_WithNoDisplayOrder_FallsBackToUploadedAtDesc()
    {
        var productId = Guid.NewGuid();
        var newerFileId = Guid.NewGuid();
        var olderFileId = Guid.NewGuid();
        var repository = new FakeProductRepository(
            products: [],
            details:
            [
                new ProductDetailReadModel
                {
                    ProductId = productId,
                    ProductName = "Modern Floor Lamp",
                    Status = ProductStatus.ACTIVE
                }
            ]);
        var baseTime = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var files = new FakeCatalogProjectFileRepository
        {
            CatalogFiles =
            [
                CreateCatalogFile(productId, "PRODUCT", olderFileId, Guid.NewGuid(), FileType.PRODUCT_PREVIEW, "older.jpg", uploadedAt: baseTime.AddHours(-1)),
                CreateCatalogFile(productId, "PRODUCT", newerFileId, Guid.NewGuid(), FileType.PRODUCT_PREVIEW, "newer.jpg", uploadedAt: baseTime)
            ]
        };
        var service = CatalogServiceTestHelper.CreateProductService(repository, files);

        var result = await service.GetByIdAsync(productId);

        Assert.Equal(200, result.Status);
        Assert.Equal(2, result.Data!.Files.Count);
        Assert.Equal(newerFileId, result.Data.Files[0].FileId);
        Assert.Equal(olderFileId, result.Data.Files[1].FileId);
    }

    private static CatalogFileReadModel CreateCatalogFile(
        Guid referenceId,
        string referenceType,
        Guid fileId,
        Guid fileLinkId,
        FileType fileType,
        string fileName,
        string mimeType = "image/jpeg",
        int? displayOrder = null,
        bool? isPrimary = null,
        DateTime? uploadedAt = null)
    {
        return new CatalogFileReadModel
        {
            FileId = fileId,
            FileLinkId = fileLinkId,
            ReferenceId = referenceId,
            ReferenceType = referenceType,
            FileType = fileType,
            OriginalFileName = fileName,
            FileUrl = $"https://storage.example.com/{fileName}",
            MimeType = mimeType,
            FileSizeBytes = 1024,
            Visibility = FileVisibility.CUSTOMER_VISIBLE,
            Status = FileStatus.ACTIVE,
            DisplayOrder = displayOrder,
            IsPrimary = isPrimary,
            UploadedAt = uploadedAt ?? DateTime.UtcNow
        };
    }

    [Fact]
    public async Task SearchAsync_WithInvalidSort_ReturnsBadRequest()
    {
        var repository = new FakeProductRepository([]);
        var service = CatalogServiceTestHelper.CreateProductService(repository, new FakeCatalogProjectFileRepository());

        var result = await service.SearchAsync(new ProductSearchRequestDto
        {
            Sort = "invalid",
            Page = 1,
            Limit = 20
        });

        Assert.Equal(400, result.Status);
        Assert.Contains("Sort must be one of", result.Message);
    }

    [Fact]
    public async Task SearchAsync_WhenElasticsearchUnavailable_FallsBackToRepository()
    {
        var productId = Guid.NewGuid();
        var repository = new FakeProductRepository(
        [
            new ProductListItemReadModel
            {
                ProductId = productId,
                ProductName = "Oak Desk",
                Status = ProductStatus.ACTIVE,
                DefaultVersion = new ProductVersionReadModel
                {
                    ProductVersionId = Guid.NewGuid(),
                    VersionCode = "V1",
                    VersionName = "Standard",
                    Status = ProductStatus.ACTIVE,
                    IsPublic = true
                }
            }
        ]);

        var search = new ThrowingSearchIndexService();
        var service = CatalogServiceTestHelper.CreateProductService(
            repository,
            new FakeCatalogProjectFileRepository(),
            search: search);

        var result = await service.SearchAsync(new ProductSearchRequestDto
        {
            Query = "Oak",
            Page = 1,
            Limit = 20
        });

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data!.Items);
        Assert.Equal(productId, result.Data.Items[0].ProductId);
    }

    [Fact]
    public async Task SuggestAsync_WhenElasticsearchUnavailable_FallsBackToRepository()
    {
        var productId = Guid.NewGuid();
        var repository = new FakeProductRepository(
        [
            new ProductListItemReadModel
            {
                ProductId = productId,
                ProductName = "Oak Desk",
                Status = ProductStatus.ACTIVE,
                DefaultVersion = new ProductVersionReadModel
                {
                    ProductVersionId = Guid.NewGuid(),
                    VersionCode = "V1",
                    VersionName = "Standard",
                    Status = ProductStatus.ACTIVE,
                    IsPublic = true
                }
            }
        ]);

        var service = CatalogServiceTestHelper.CreateProductService(
            repository,
            new FakeCatalogProjectFileRepository(),
            search: new ThrowingSearchIndexService());

        var result = await service.SuggestAsync("Oak", limit: 10);

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data!.Items);
        Assert.Equal(productId, result.Data.Items[0].ProductId);
        Assert.Equal("Oak Desk", result.Data.Items[0].ProductName);
    }

    [Theory]
    [InlineData("", 10, "Query is required.")]
    [InlineData("   ", 10, "Query is required.")]
    [InlineData("Oak", 0, "Limit must be between 1 and 20.")]
    [InlineData("Oak", 21, "Limit must be between 1 and 20.")]
    public async Task SuggestAsync_WithInvalidInput_ReturnsBadRequest(
        string query,
        int limit,
        string expectedMessage)
    {
        var repository = new FakeProductRepository([]);
        var service = CatalogServiceTestHelper.CreateProductService(repository, new FakeCatalogProjectFileRepository());

        var result = await service.SuggestAsync(query, limit);

        Assert.Equal(400, result.Status);
        Assert.Equal(expectedMessage, result.Message);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task GetSimilarAsync_WhenElasticsearchUnavailable_FallsBackToRepository()
    {
        var categoryId = Guid.NewGuid();
        var sourceProductId = Guid.NewGuid();
        var similarProductId = Guid.NewGuid();
        var repository = new FakeProductRepository(
        [
            new ProductListItemReadModel
            {
                ProductId = sourceProductId,
                CategoryId = categoryId,
                ProductName = "Oak Desk",
                Status = ProductStatus.ACTIVE
            },
            new ProductListItemReadModel
            {
                ProductId = similarProductId,
                CategoryId = categoryId,
                ProductName = "Oak Shelf",
                Status = ProductStatus.ACTIVE,
                DefaultVersion = new ProductVersionReadModel
                {
                    ProductVersionId = Guid.NewGuid(),
                    VersionCode = "V1",
                    VersionName = "Standard",
                    Status = ProductStatus.ACTIVE,
                    IsPublic = true
                }
            }
        ]);
        var service = CatalogServiceTestHelper.CreateProductService(
            repository,
            new FakeCatalogProjectFileRepository(),
            search: new ThrowingSearchIndexService());

        var result = await service.GetSimilarAsync(sourceProductId, limit: 4);

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data.Total);
        var item = Assert.Single(result.Data.Items);
        Assert.Equal(similarProductId, item.ProductId);
        Assert.Equal("Oak Shelf", item.ProductName);
        Assert.Equal(1, result.Data.Page);
        Assert.Equal(4, result.Data.Limit);
    }

    [Theory]
    [InlineData(0, "Product id is required.")]
    [InlineData(21, "Limit must be between 1 and 20.")]
    public async Task GetSimilarAsync_WithInvalidInput_ReturnsBadRequest(
        int limit,
        string expectedMessage)
    {
        var productId = limit == 0 ? Guid.Empty : Guid.NewGuid();
        var repository = new FakeProductRepository([]);
        var service = CatalogServiceTestHelper.CreateProductService(repository, new FakeCatalogProjectFileRepository());

        var result = await service.GetSimilarAsync(productId, limit);

        Assert.Equal(400, result.Status);
        Assert.Equal(expectedMessage, result.Message);
        Assert.Null(result.Data);
    }

    private sealed class ThrowingSearchIndexService : ISearchIndexService
    {
        public Task IndexAsync<TDocument>(string indexName, string id, TDocument document, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task BulkIndexAsync<TDocument>(string indexName, IReadOnlyList<BulkIndexItem<TDocument>> items, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(string indexName, string id, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<SearchResult<TDocument>> SearchAsync<TDocument>(string indexName, SearchRequest request, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Elasticsearch unavailable.");

        public Task<IReadOnlyList<TDocument>> SearchAsync<TDocument>(string indexName, string query, int size = 100, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Elasticsearch unavailable.");

        public Task<SuggestResult> SuggestAsync(string indexName, SuggestRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new SuggestResult());

        public Task<SearchResult<TDocument>> MoreLikeThisAsync<TDocument>(
            string indexName,
            string documentId,
            MoreLikeThisRequest request,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Elasticsearch unavailable.");

        public Task<SearchAggregationResult> AggregateAsync(
            string indexName,
            SearchAggregationRequest request,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Elasticsearch unavailable.");
    }

    private sealed class FakeProductRepository : IProductRepository
    {
        private readonly IReadOnlyList<ProductListItemReadModel> _products;
        private readonly IReadOnlyList<ProductCategoryReadModel> _categories;
        private readonly IReadOnlyList<ProductDetailReadModel> _details;
        private readonly List<Product> _entities;
        private readonly List<Product> _createdProducts = [];

        public FakeProductRepository(
            IReadOnlyList<ProductListItemReadModel> products,
            IReadOnlyList<ProductCategoryReadModel>? categories = null,
            IReadOnlyList<ProductDetailReadModel>? details = null,
            IReadOnlyList<Product>? entities = null)
        {
            _products = products;
            _categories = categories ?? [];
            _details = details ?? [];
            _entities = entities?.ToList() ?? [];
        }

        public int GetDetailCallCount { get; private set; }
        public int GetByIdCallCount { get; private set; }
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

        public Task<ProductListItemReadModel?> GetSearchIndexItemAsync(
            Guid productId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_products.FirstOrDefault(product => product.ProductId == productId));
        }

        public Task<IReadOnlyList<ProductListItemReadModel>> GetSearchIndexPageAsync(
            int page,
            int limit,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ProductListItemReadModel>>(
                _products
                    .Skip((page - 1) * limit)
                    .Take(limit)
                    .ToList());
        }

        public Task<ProductSearchResultReadModel> SearchPublicAsync(
            ProductSearchQueryReadModel query,
            CancellationToken cancellationToken = default)
        {
            var items = _products
                .Where(product => !query.CategoryId.HasValue || product.CategoryId == query.CategoryId)
                .Where(product => string.IsNullOrWhiteSpace(query.Query) ||
                    product.ProductName.Contains(query.Query, StringComparison.OrdinalIgnoreCase))
                .Skip((query.Page - 1) * query.Limit)
                .Take(query.Limit)
                .ToList();

            return Task.FromResult(new ProductSearchResultReadModel
            {
                Items = items,
                Total = items.Count
            });
        }

        public Task<IReadOnlyList<ProductListItemReadModel>> SuggestPublicAsync(
            string query,
            int limit,
            CancellationToken cancellationToken = default)
        {
            var items = _products
                .Where(product => product.ProductName.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Take(limit)
                .ToList();

            return Task.FromResult<IReadOnlyList<ProductListItemReadModel>>(items);
        }

        public Task<IReadOnlyList<ProductListItemReadModel>> GetSimilarPublicAsync(
            Guid productId,
            int limit,
            CancellationToken cancellationToken = default)
        {
            var source = _products.FirstOrDefault(product => product.ProductId == productId);
            if (source is null)
            {
                return Task.FromResult<IReadOnlyList<ProductListItemReadModel>>([]);
            }

            var items = _products
                .Where(product => product.ProductId != productId && product.CategoryId == source.CategoryId)
                .Take(limit)
                .ToList();

            return Task.FromResult<IReadOnlyList<ProductListItemReadModel>>(items);
        }

        public IQueryable<Product> Query() => Enumerable.Empty<Product>().AsQueryable();
        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            GetByIdCallCount++;
            return Task.FromResult(_entities.FirstOrDefault(product => product.ProductId == id));
        }

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
