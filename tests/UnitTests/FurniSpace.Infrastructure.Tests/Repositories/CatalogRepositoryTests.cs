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

public sealed class CatalogRepositoryTests
{
    [Fact]
    public async Task GetProjectCatalogAsync_FiltersByKeywordCaseInsensitively()
    {
        await using var context = CreateContext();
        var projectId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var productA = CreateProduct("PM-001", "Coffee Counter", categoryId);
        var productB = CreateProduct("PM-002", "Bar Stool", categoryId);
        context.ProductSet.AddRange(productA, productB);
        context.ProductVersionSet.AddRange(
            CreateVersion(productA.ProductId, "PV-PUBLIC-A", isPublic: true),
            CreateVersion(productB.ProductId, "PV-PUBLIC-B", isPublic: true));
        await context.SaveChangesAsync();
        var repository = new CatalogRepository(context);

        var items = await repository.GetProjectCatalogAsync(new ProjectCatalogQueryReadModel
        {
            ProjectId = projectId,
            Page = 1,
            PageSize = 20,
            Keyword = "coffee"
        });

        Assert.Single(items);
        Assert.Equal("Coffee Counter", items[0].ProductName);
    }

    [Fact]
    public async Task GetAdminVersionListAsync_ReturnsVersionsForProduct()
    {
        await using var context = CreateContext();
        var productId = Guid.NewGuid();
        context.ProductSet.Add(CreateProduct("PM-001", "Counter", Guid.NewGuid(), productId));
        context.ProductVersionSet.AddRange(
            CreateVersion(productId, "PV-001", isDefault: true),
            CreateVersion(productId, "PV-002", status: ProductStatus.INACTIVE));
        await context.SaveChangesAsync();
        var repository = new CatalogRepository(context);

        var items = await repository.GetAdminVersionListAsync(new ProductVersionListQueryReadModel
        {
            ProductId = productId,
            Page = 1,
            PageSize = 20
        });

        Assert.Equal(2, items.Count);
        Assert.Equal("PV-001", items[0].VersionCode);
        Assert.True(items[0].IsDefault);
    }

    [Fact]
    public async Task GetProjectCatalogAsync_ReturnsOnlyEligiblePublicVersions()
    {
        await using var context = CreateContext();
        var projectId = Guid.NewGuid();
        var otherProjectId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        context.ProductSet.Add(CreateProduct("PM-001", "Counter", Guid.NewGuid(), productId));
        context.ProductVersionSet.AddRange(
            CreateVersion(productId, "PV-PUBLIC", isPublic: true),
            CreateVersion(productId, "PV-PRIVATE", isPublic: false),
            CreateVersion(
                productId,
                "PV-PROJECT",
                isPublic: false,
                isProjectSpecific: true,
                projectId: projectId),
            CreateVersion(
                productId,
                "PV-OTHER",
                isPublic: false,
                isProjectSpecific: true,
                projectId: otherProjectId));
        await context.SaveChangesAsync();
        var repository = new CatalogRepository(context);

        var items = await repository.GetProjectCatalogAsync(new ProjectCatalogQueryReadModel
        {
            ProjectId = projectId,
            Page = 1,
            PageSize = 20
        });

        Assert.Single(items);
        Assert.Equal(2, items[0].EligibleVersions.Count);
        Assert.Contains(items[0].EligibleVersions, version => version.VersionCode == "PV-PUBLIC");
        Assert.Contains(items[0].EligibleVersions, version => version.VersionCode == "PV-PROJECT");
    }

    [Fact]
    public async Task CountActiveVersionsByProductAsync_CountsOnlyActiveVersions()
    {
        await using var context = CreateContext();
        var productId = Guid.NewGuid();
        context.ProductSet.Add(CreateProduct("PM-001", "Counter", Guid.NewGuid(), productId));
        context.ProductVersionSet.AddRange(
            CreateVersion(productId, "PV-ACTIVE", status: ProductStatus.ACTIVE),
            CreateVersion(productId, "PV-INACTIVE", status: ProductStatus.INACTIVE));
        await context.SaveChangesAsync();
        var repository = new CatalogRepository(context);

        var count = await repository.CountActiveVersionsByProductAsync(productId);

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task GetProjectCatalogProductDetailAsync_ReturnsEligibleProduct()
    {
        await using var context = CreateContext();
        var projectId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        context.ProductSet.Add(CreateProduct("PM-001", "Counter", Guid.NewGuid(), productId));
        context.ProductVersionSet.Add(CreateVersion(productId, "PV-PUBLIC", isPublic: true));
        await context.SaveChangesAsync();
        var repository = new CatalogRepository(context);

        var item = await repository.GetProjectCatalogProductDetailAsync(projectId, productId);

        Assert.NotNull(item);
        Assert.Equal("Counter", item!.ProductName);
        Assert.Single(item.EligibleVersions);
    }

    [Fact]
    public async Task GetProjectEligibleVersionDetailAsync_ReturnsEligibleVersion()
    {
        await using var context = CreateContext();
        var projectId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        context.ProductSet.Add(CreateProduct("PM-001", "Counter", Guid.NewGuid(), productId));
        context.ProductVersionSet.Add(CreateVersion(
            productId,
            "PV-PUBLIC",
            versionId: versionId,
            isPublic: true,
            material: "Wood",
            estimatedPrice: 1000m));
        await context.SaveChangesAsync();
        var repository = new CatalogRepository(context);

        var version = await repository.GetProjectEligibleVersionDetailAsync(projectId, versionId);

        Assert.NotNull(version);
        Assert.Equal("PV-PUBLIC", version!.VersionCode);
        Assert.Equal("Wood", version.Material);
        Assert.Equal(1000m, version.EstimatedPrice);
    }

    [Fact]
    public async Task GetProjectEligibleVersionDetailAsync_WithIneligibleVersion_ReturnsNull()
    {
        await using var context = CreateContext();
        var projectId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        context.ProductSet.Add(CreateProduct("PM-001", "Counter", Guid.NewGuid(), productId));
        context.ProductVersionSet.Add(CreateVersion(
            productId,
            "PV-PRIVATE",
            versionId: versionId,
            isPublic: false));
        await context.SaveChangesAsync();
        var repository = new CatalogRepository(context);

        var version = await repository.GetProjectEligibleVersionDetailAsync(projectId, versionId);

        Assert.Null(version);
    }

    [Fact]
    public async Task CountProjectCatalogAsync_ReturnsFilteredTotal()
    {
        await using var context = CreateContext();
        var projectId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        context.CategorySet.Add(new Category { CategoryId = categoryId, CategoryName = "Counter", Status = ProductStatus.ACTIVE });
        var productA = CreateProduct("PM-001", "Counter A", categoryId);
        var productB = CreateProduct("PM-002", "Counter B", categoryId);
        context.ProductSet.AddRange(productA, productB);
        context.ProductVersionSet.AddRange(
            CreateVersion(productA.ProductId, "PV-A", isPublic: true),
            CreateVersion(productB.ProductId, "PV-B", isPublic: true));
        await context.SaveChangesAsync();
        var repository = new CatalogRepository(context);

        var total = await repository.CountProjectCatalogAsync(new ProjectCatalogQueryReadModel
        {
            ProjectId = projectId,
            CategoryId = categoryId,
            Page = 1,
            PageSize = 20
        });

        Assert.Equal(2, total);
    }

    [Fact]
    public async Task GetAdminVersionListAsync_FiltersByStatusAndDefault()
    {
        await using var context = CreateContext();
        var productId = Guid.NewGuid();
        context.ProductSet.Add(CreateProduct("PM-001", "Counter", Guid.NewGuid(), productId));
        context.ProductVersionSet.AddRange(
            CreateVersion(productId, "PV-ACTIVE-DEFAULT", isDefault: true, status: ProductStatus.ACTIVE),
            CreateVersion(productId, "PV-INACTIVE", status: ProductStatus.INACTIVE));
        await context.SaveChangesAsync();
        var repository = new CatalogRepository(context);

        var activeItems = await repository.GetAdminVersionListAsync(new ProductVersionListQueryReadModel
        {
            ProductId = productId,
            Status = ProductStatus.ACTIVE,
            Page = 1,
            PageSize = 20
        });
        var defaultItems = await repository.GetAdminVersionListAsync(new ProductVersionListQueryReadModel
        {
            ProductId = productId,
            IsDefault = true,
            Page = 1,
            PageSize = 20
        });

        Assert.Single(activeItems);
        Assert.Equal("PV-ACTIVE-DEFAULT", activeItems[0].VersionCode);
        Assert.Single(defaultItems);
        Assert.True(defaultItems[0].IsDefault);
    }

    [Fact]
    public async Task CountAdminVersionListAsync_ReturnsFilteredCount()
    {
        await using var context = CreateContext();
        var productId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        context.ProductSet.Add(CreateProduct("PM-001", "Counter", Guid.NewGuid(), productId));
        context.ProductVersionSet.AddRange(
            CreateVersion(productId, "PV-STANDARD", isPublic: true),
            CreateVersion(
                productId,
                "PV-PROJECT",
                isPublic: false,
                isProjectSpecific: true,
                projectId: projectId));
        await context.SaveChangesAsync();
        var repository = new CatalogRepository(context);

        var count = await repository.CountAdminVersionListAsync(new ProductVersionListQueryReadModel
        {
            ProductId = productId,
            IsProjectSpecific = true,
            ProjectId = projectId,
            Page = 1,
            PageSize = 20
        });

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task GetAdminCatalogAsync_ReturnsProductsWithVersionSummary()
    {
        await using var context = CreateContext();
        var categoryId = Guid.NewGuid();
        context.CategorySet.Add(new Category
        {
            CategoryId = categoryId,
            CategoryName = "Counters",
            Status = ProductStatus.ACTIVE
        });
        var productId = Guid.NewGuid();
        context.ProductSet.Add(CreateProduct("PM-001", "Coffee Counter", categoryId, productId));
        context.ProductVersionSet.AddRange(
            CreateVersion(productId, "PV-DEFAULT", isDefault: true, status: ProductStatus.ACTIVE, estimatedPrice: 1200m),
            CreateVersion(productId, "PV-INACTIVE", status: ProductStatus.INACTIVE));
        await context.SaveChangesAsync();
        var repository = new CatalogRepository(context);

        var items = await repository.GetAdminCatalogAsync(new AdminCatalogQueryReadModel
        {
            Page = 1,
            PageSize = 20,
            SortBy = "productName",
            SortDirection = "asc"
        });

        Assert.Single(items);
        var item = items[0];
        Assert.Equal("Coffee Counter", item.ProductName);
        Assert.Equal("Counters", item.CategoryName);
        Assert.Equal(2, item.TotalVersionCount);
        Assert.Equal(1, item.ActiveVersionCount);
        Assert.Equal(1, item.InactiveVersionCount);
        Assert.Equal("PV-DEFAULT", item.DefaultVersionCode);
        Assert.Equal(1200m, item.DefaultVersionEstimatedPrice);
    }

    [Fact]
    public async Task CountAdminCatalogAsync_FiltersByHasActiveVersion()
    {
        await using var context = CreateContext();
        var categoryId = Guid.NewGuid();
        var activeProductId = Guid.NewGuid();
        var inactiveProductId = Guid.NewGuid();
        context.ProductSet.AddRange(
            CreateProduct("PM-001", "With Active Version", categoryId, activeProductId),
            CreateProduct("PM-002", "Without Active Version", categoryId, inactiveProductId));
        context.ProductVersionSet.AddRange(
            CreateVersion(activeProductId, "PV-ACTIVE", status: ProductStatus.ACTIVE),
            CreateVersion(inactiveProductId, "PV-INACTIVE", status: ProductStatus.INACTIVE));
        await context.SaveChangesAsync();
        var repository = new CatalogRepository(context);

        var withActiveCount = await repository.CountAdminCatalogAsync(new AdminCatalogQueryReadModel
        {
            HasActiveVersion = true,
            Page = 1,
            PageSize = 20
        });
        var withoutActiveCount = await repository.CountAdminCatalogAsync(new AdminCatalogQueryReadModel
        {
            HasActiveVersion = false,
            Page = 1,
            PageSize = 20
        });

        Assert.Equal(1, withActiveCount);
        Assert.Equal(1, withoutActiveCount);
    }

    [Fact]
    public async Task GetAdminCatalogAsync_FiltersByVersionStatus()
    {
        await using var context = CreateContext();
        var categoryId = Guid.NewGuid();
        var matchingProductId = Guid.NewGuid();
        var otherProductId = Guid.NewGuid();
        context.ProductSet.AddRange(
            CreateProduct("PM-001", "Matching Product", categoryId, matchingProductId),
            CreateProduct("PM-002", "Other Product", categoryId, otherProductId));
        context.ProductVersionSet.AddRange(
            CreateVersion(matchingProductId, "PV-ARCHIVED", status: ProductStatus.ARCHIVED),
            CreateVersion(otherProductId, "PV-ACTIVE", status: ProductStatus.ACTIVE));
        await context.SaveChangesAsync();
        var repository = new CatalogRepository(context);

        var items = await repository.GetAdminCatalogAsync(new AdminCatalogQueryReadModel
        {
            VersionStatus = ProductStatus.ARCHIVED,
            Page = 1,
            PageSize = 20
        });

        Assert.Single(items);
        Assert.Equal(matchingProductId, items[0].ProductId);
    }

    [Fact]
    public async Task GetAdminCatalogAsync_FiltersByHas3DModelOnProduct()
    {
        await using var context = CreateContext();
        var categoryId = Guid.NewGuid();
        var withModelProductId = Guid.NewGuid();
        var withoutModelProductId = Guid.NewGuid();
        context.ProductSet.AddRange(
            CreateProduct("PM-001", "With Model", categoryId, withModelProductId),
            CreateProduct("PM-002", "Without Model", categoryId, withoutModelProductId));
        context.ProductVersionSet.Add(CreateVersion(withModelProductId, "PV-001"));
        context.ProductVersionSet.Add(CreateVersion(withoutModelProductId, "PV-002"));
        context.FileLinkSet.Add(new FileLink
        {
            FileLinkId = Guid.NewGuid(),
            FileId = Guid.NewGuid(),
            ReferenceType = "PRODUCT",
            ReferenceId = withModelProductId,
            FileType = FileType.MODEL_3D
        });
        await context.SaveChangesAsync();
        var repository = new CatalogRepository(context);

        var withModelItems = await repository.GetAdminCatalogAsync(new AdminCatalogQueryReadModel
        {
            Has3DModel = true,
            Page = 1,
            PageSize = 20
        });
        var withoutModelItems = await repository.GetAdminCatalogAsync(new AdminCatalogQueryReadModel
        {
            Has3DModel = false,
            Page = 1,
            PageSize = 20
        });

        Assert.Single(withModelItems);
        Assert.Equal(withModelProductId, withModelItems[0].ProductId);
        Assert.Single(withoutModelItems);
        Assert.Equal(withoutModelProductId, withoutModelItems[0].ProductId);
    }

    [Fact]
    public async Task GetAdminCatalogAsync_FiltersByCategoryBusinessTypeAndVersionType()
    {
        await using var context = CreateContext();
        var categoryId = Guid.NewGuid();
        var otherCategoryId = Guid.NewGuid();
        context.CategorySet.AddRange(
            new Category { CategoryId = categoryId, CategoryName = "Counters", Status = ProductStatus.ACTIVE },
            new Category { CategoryId = otherCategoryId, CategoryName = "Other", Status = ProductStatus.ACTIVE });
        var matchingProductId = Guid.NewGuid();
        var otherProductId = Guid.NewGuid();
        context.ProductSet.AddRange(
            new Product
            {
                ProductId = matchingProductId,
                CategoryId = categoryId,
                ProductCode = "PM-001",
                ProductName = "Matching Product",
                BusinessTypeIds = [1, 2],
                Status = ProductStatus.ACTIVE,
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                UpdatedAt = DateTime.UtcNow
            },
            new Product
            {
                ProductId = otherProductId,
                CategoryId = otherCategoryId,
                ProductCode = "PM-002",
                ProductName = "Other Product",
                BusinessTypeIds = [3],
                Status = ProductStatus.ACTIVE,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        context.ProductVersionSet.AddRange(
            CreateVersion(
                matchingProductId,
                "PV-STANDARD",
                versionType: ProductVersionType.STANDARD),
            CreateVersion(
                otherProductId,
                "PV-PROJECT",
                isProjectSpecific: true,
                projectId: Guid.NewGuid()));
        await context.SaveChangesAsync();
        var repository = new CatalogRepository(context);

        var items = await repository.GetAdminCatalogAsync(new AdminCatalogQueryReadModel
        {
            CategoryId = categoryId,
            BusinessTypeId = 2,
            VersionType = ProductVersionType.STANDARD,
            CreatedFrom = DateTime.UtcNow.AddDays(-3),
            CreatedTo = DateTime.UtcNow.AddDays(-1),
            SortBy = "updatedAt",
            SortDirection = "desc",
            Page = 1,
            PageSize = 20
        });

        Assert.Single(items);
        Assert.Equal(matchingProductId, items[0].ProductId);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static Product CreateProduct(
        string code,
        string name,
        Guid categoryId,
        Guid? productId = null)
    {
        return new Product
        {
            ProductId = productId ?? Guid.NewGuid(),
            CategoryId = categoryId,
            ProductCode = code,
            ProductName = name,
            Status = ProductStatus.ACTIVE,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static ProductVersion CreateVersion(
        Guid productId,
        string versionCode,
        ProductStatus status = ProductStatus.ACTIVE,
        bool isPublic = false,
        bool isProjectSpecific = false,
        Guid? projectId = null,
        bool isDefault = false,
        Guid? versionId = null,
        string? material = null,
        decimal? estimatedPrice = null,
        ProductVersionType? versionType = null)
    {
        var isProject = isProjectSpecific || versionType == ProductVersionType.PROJECT_SPECIFIC;
        return new ProductVersion
        {
            ProductVersionId = versionId ?? Guid.NewGuid(),
            ProductId = productId,
            ProjectId = projectId,
            VersionCode = versionCode,
            VersionName = versionCode,
            VersionType = versionType ?? (isProject ? ProductVersionType.PROJECT_SPECIFIC : ProductVersionType.STANDARD),
            Material = material,
            EstimatedPrice = estimatedPrice ?? 10_000_000m,
            Status = status,
            IsPublic = isPublic,
            IsProjectSpecific = isProject,
            IsDefault = isDefault,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
