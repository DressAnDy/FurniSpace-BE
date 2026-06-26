#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.Repositories.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.Repositories;

public sealed class ProductVersionRepositoryTests
{
    [Fact]
    public async Task GetValidDetailsAsync_ReturnsOnlyActivePublicOrProjectSpecificVersions()
    {
        await using var context = CreateContext();
        var projectId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var publicVersionId = Guid.NewGuid();
        var projectVersionId = Guid.NewGuid();
        var otherProjectVersionId = Guid.NewGuid();
        var inactiveVersionId = Guid.NewGuid();
        context.ProductSet.Add(new Product
        {
            ProductId = productId,
            ProductName = "Cafe Chair",
            Status = ProductStatus.ACTIVE
        });
        context.ProductVersionSet.AddRange(
            CreateVersion(publicVersionId, productId, isPublic: true, status: ProductStatus.ACTIVE),
            CreateVersion(projectVersionId, productId, projectId, isProjectSpecific: true, status: ProductStatus.ACTIVE),
            CreateVersion(otherProjectVersionId, productId, Guid.NewGuid(), isProjectSpecific: true, status: ProductStatus.ACTIVE),
            CreateVersion(inactiveVersionId, productId, isPublic: true, status: ProductStatus.INACTIVE));
        await context.SaveChangesAsync();
        var repository = new ProductVersionRepository(context);

        var result = await repository.GetValidDetailsAsync(
            [publicVersionId, projectVersionId, otherProjectVersionId, inactiveVersionId],
            projectId);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, item => item.ProductVersionId == publicVersionId && item.ProductName == "Cafe Chair");
        Assert.Contains(result, item => item.ProductVersionId == projectVersionId);
        Assert.DoesNotContain(result, item => item.ProductVersionId == otherProjectVersionId);
        Assert.DoesNotContain(result, item => item.ProductVersionId == inactiveVersionId);
    }

    [Fact]
    public async Task GetValidDetailsAsync_WithNoIds_ReturnsEmptyList()
    {
        await using var context = CreateContext();
        var repository = new ProductVersionRepository(context);

        var result = await repository.GetValidDetailsAsync([], Guid.NewGuid());

        Assert.Empty(result);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static ProductVersion CreateVersion(
        Guid versionId,
        Guid productId,
        Guid? projectId = null,
        bool isPublic = false,
        bool isProjectSpecific = false,
        ProductStatus status = ProductStatus.ACTIVE)
    {
        return new ProductVersion
        {
            ProductVersionId = versionId,
            ProductId = productId,
            ProjectId = projectId,
            VersionCode = $"PV-{versionId:N}",
            VersionName = "Brown Wood",
            VersionType = ProductVersionType.STANDARD,
            EstimatedPrice = 1200000m,
            IsPublic = isPublic,
            IsProjectSpecific = isProjectSpecific,
            Status = status
        };
    }
}
