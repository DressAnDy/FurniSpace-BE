#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.ReadModels.Products;
using FurniSpace.Infrastructure.Repositories.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.Repositories;

public sealed class ProductRepositoryTests
{
    [Fact]
    public async Task DetailListAndCategoryQueries_ReturnProjectedProducts()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new ProductRepository(context);

        var codeExists = await repository.ProductCodeExistsAsync("TBL-001");
        var codeMissing = await repository.ProductCodeExistsAsync("MISSING");
        var detail = await repository.GetDetailAsync(data.TableId);
        var category = await repository.GetCategoryAsync(data.TableCategoryId);
        var list = await repository.GetPublicListAsync(page: 1, limit: 10);
        var byCategory = await repository.GetPublicListByCategoryAsync(
            data.TableCategoryId,
            page: 1,
            limit: 10,
            includeDefaultVersion: false);
        var count = await repository.CountAsync();
        var categoryCount = await repository.CountByCategoryAsync(data.TableCategoryId);

        Assert.True(codeExists);
        Assert.False(codeMissing);
        Assert.NotNull(detail);
        Assert.Equal(data.TableId, detail.ProductId);
        Assert.Equal("Tables", detail.CategoryName);
        Assert.Equal(3, detail.Versions.Count);
        Assert.Equal("Oak", detail.DefaultVersion?.Material);
        Assert.NotNull(category);
        Assert.Equal("Tables", category.CategoryName);
        Assert.Equal(4, count);
        Assert.Equal(2, categoryCount);
        Assert.Equal(4, list.Count);
        Assert.Contains(list, product => product.ProductId == data.TableId && product.DefaultVersion?.Material == "Oak");
        Assert.All(byCategory, product =>
        {
            Assert.Equal(data.TableCategoryId, product.CategoryId);
            Assert.Null(product.DefaultVersion);
        });
    }

    [Fact]
    public async Task SearchIndexQueries_ReturnPagedProjection()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new ProductRepository(context);

        var item = await repository.GetSearchIndexItemAsync(data.TableId);
        var page = await repository.GetSearchIndexPageAsync(page: 1, limit: 2);

        Assert.NotNull(item);
        Assert.Equal("Dining Table", item.ProductName);
        Assert.Equal("Oak", item.DefaultVersion?.Material);
        Assert.Equal(2, page.Count);
        Assert.True(page[0].CreatedAtOrMin() >= page[1].CreatedAtOrMin());
    }

    [Fact]
    public async Task SearchPublicAsync_FiltersByTextAttributesPriceAndSorts()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new ProductRepository(context);

        var result = await repository.SearchPublicAsync(new ProductSearchQueryReadModel
        {
            Query = "table",
            CategoryId = data.TableCategoryId,
            Material = "oak",
            Color = "brown",
            MinPrice = 100,
            MaxPrice = 300,
            Sort = "price_desc",
            Page = 1,
            Limit = 10
        });
        var createdAscending = await repository.SearchPublicAsync(new ProductSearchQueryReadModel
        {
            Sort = "created_asc",
            Page = 1,
            Limit = 10
        });

        Assert.Equal(2, result.Total);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(data.TableId, result.Items[0].ProductId);
        Assert.Equal(4, createdAscending.Total);
        Assert.Equal("Dining Table", createdAscending.Items[0].ProductName);
    }

    [Fact]
    public async Task BusinessTypeFilter_UsesAnySemanticsAndExcludesMissingAssignments()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new ProductRepository(context);

        var filteredList = await repository.GetPublicListAsync(
            page: 1,
            limit: 10,
            businessTypeIds: [2, 1, 2]);
        var filteredSearch = await repository.SearchPublicAsync(new ProductSearchQueryReadModel
        {
            BusinessTypeIds = [2],
            Page = 1,
            Limit = 10
        });
        var filteredCount = await repository.CountAsync([2]);

        Assert.Equal(2, filteredList.Count);
        Assert.Contains(filteredList, product => product.ProductId == data.TableId);
        Assert.Contains(filteredList, product => product.ProductId == data.SideTableId);
        Assert.Single(filteredSearch.Items);
        Assert.Equal(data.SideTableId, filteredSearch.Items[0].ProductId);
        Assert.Equal(1, filteredCount);
    }

    [Fact]
    public async Task SuggestAndSimilar_ReturnRankedPublicProducts()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new ProductRepository(context);

        var suggestions = await repository.SuggestPublicAsync("ta", limit: 5);
        var similar = await repository.GetSimilarPublicAsync(data.TableId, limit: 5);
        var noSimilar = await repository.GetSimilarPublicAsync(Guid.NewGuid(), limit: 5);

        Assert.Contains(suggestions, product => product.ProductName == "Dining Table");
        Assert.Contains(suggestions, product => product.ProductName == "Side Table");
        Assert.Single(similar);
        Assert.Equal(data.SideTableId, similar[0].ProductId);
        Assert.Empty(noSimilar);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<SeededData> SeedAsync(AppDbContext context)
    {
        var tableCategoryId = Guid.NewGuid();
        var chairCategoryId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var sideTableId = Guid.NewGuid();
        var chairId = Guid.NewGuid();
        var stoolId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        context.CategorySet.AddRange(
            new Category
            {
                CategoryId = tableCategoryId,
                CategoryName = "Tables",
                Status = ProductStatus.ACTIVE
            },
            new Category
            {
                CategoryId = chairCategoryId,
                CategoryName = "Chairs",
                Status = ProductStatus.ACTIVE
            });

        context.ProductSet.AddRange(
            CreateProduct(tableId, tableCategoryId, "TBL-001", "Dining Table", "Oak table for cafe", now.AddDays(-3)),
            CreateProduct(sideTableId, tableCategoryId, "TBL-002", "Side Table", "Compact brown table", now.AddDays(-2)),
            CreateProduct(chairId, chairCategoryId, "CHR-001", "Cafe Chair", "Walnut seating", now.AddDays(-1)),
            CreateProduct(stoolId, chairCategoryId, "STL-001", "Cafe Stool", "Backless seating", now, []));

        context.ProductSet.Find(tableId)!.BusinessTypeIds = [1];
        context.ProductSet.Find(sideTableId)!.BusinessTypeIds = [2];

        context.ProductVersionSet.AddRange(
            CreateVersion(tableId, "Oak", "Brown", 200m, isDefault: true, isPublic: true, status: ProductStatus.ACTIVE, now.AddDays(-3)),
            CreateVersion(tableId, "Hidden", "Gray", 50m, isDefault: false, isPublic: false, status: ProductStatus.ACTIVE, now.AddDays(-2)),
            CreateVersion(tableId, "Old", "Black", 30m, isDefault: false, isPublic: true, status: ProductStatus.INACTIVE, now.AddDays(-1)),
            CreateVersion(sideTableId, "Oak", "Brown", 150m, isDefault: true, isPublic: true, status: ProductStatus.ACTIVE, now.AddDays(-2)),
            CreateVersion(chairId, "Walnut", "Black", 90m, isDefault: true, isPublic: true, status: ProductStatus.ACTIVE, now.AddDays(-1)),
            CreateVersion(stoolId, "Metal", "Black", 80m, isDefault: true, isPublic: true, status: ProductStatus.ACTIVE, now));

        await context.SaveChangesAsync();
        return new SeededData(tableCategoryId, tableId, sideTableId);
    }

    private static Product CreateProduct(
        Guid productId,
        Guid categoryId,
        string code,
        string name,
        string description,
        DateTime createdAt,
        int[]? businessTypeIds = null)
    {
        return new Product
        {
            ProductId = productId,
            CategoryId = categoryId,
            BusinessTypeIds = businessTypeIds,
            ProductCode = code,
            ProductName = name,
            Description = description,
            Status = ProductStatus.ACTIVE,
            CreatedAt = createdAt
        };
    }

    private static ProductVersion CreateVersion(
        Guid productId,
        string material,
        string color,
        decimal price,
        bool isDefault,
        bool isPublic,
        ProductStatus status,
        DateTime createdAt)
    {
        return new ProductVersion
        {
            ProductVersionId = Guid.NewGuid(),
            ProductId = productId,
            VersionCode = $"PV-{Guid.NewGuid():N}",
            VersionName = $"{material} {color}",
            VersionType = ProductVersionType.STANDARD,
            Material = material,
            Color = color,
            Width = 120,
            Height = 75,
            Depth = 60,
            EstimatedPrice = price,
            IsDefault = isDefault,
            IsPublic = isPublic,
            IsProjectSpecific = false,
            Status = status,
            CreatedAt = createdAt
        };
    }

    private sealed record SeededData(Guid TableCategoryId, Guid TableId, Guid SideTableId);
}

file static class ProductRepositoryTestExtensions
{
    public static DateTime CreatedAtOrMin(this ProductListItemReadModel item)
        => item.DefaultVersion?.CreatedAt ?? DateTime.MinValue;
}
