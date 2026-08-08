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
        bool isDefault = false)
    {
        return new ProductVersion
        {
            ProductVersionId = Guid.NewGuid(),
            ProductId = productId,
            ProjectId = projectId,
            VersionCode = versionCode,
            VersionName = versionCode,
            VersionType = isProjectSpecific ? ProductVersionType.PROJECT_SPECIFIC : ProductVersionType.STANDARD,
            Status = status,
            IsPublic = isPublic,
            IsProjectSpecific = isProjectSpecific,
            IsDefault = isDefault,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
