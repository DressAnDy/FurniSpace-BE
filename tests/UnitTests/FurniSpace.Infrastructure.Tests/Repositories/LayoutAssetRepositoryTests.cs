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

public sealed class LayoutAssetRepositoryTests
{
    [Fact]
    public async Task GetPagedAsync_FiltersByTypeStatusAndSearch()
    {
        await using var context = CreateContext();
        await SeedAsync(context);
        var repository = new LayoutAssetRepository(context);

        var items = await repository.GetPagedAsync(
            LayoutAssetType.STAIR,
            LayoutAssetStatus.ACTIVE,
            search: null,
            page: 1,
            pageSize: 10);
        var count = await repository.CountAsync(
            LayoutAssetType.STAIR,
            LayoutAssetStatus.ACTIVE,
            search: null);

        Assert.Single(items);
        Assert.Equal("STAIR-001", items[0].AssetCode);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task AssetCodeExistsAsync_AndExceptAsync_DetectDuplicates()
    {
        await using var context = CreateContext();
        var assetId = await SeedAsync(context);
        var repository = new LayoutAssetRepository(context);

        Assert.True(await repository.AssetCodeExistsAsync("STAIR-001"));
        Assert.True(await repository.AssetCodeExistsExceptAsync("STAIR-001", Guid.NewGuid()));
        Assert.False(await repository.AssetCodeExistsExceptAsync("STAIR-001", assetId));
    }

    [Fact]
    public async Task GetForUpdateAsync_ReturnsTrackedEntity()
    {
        await using var context = CreateContext();
        var assetId = await SeedAsync(context);
        var repository = new LayoutAssetRepository(context);

        var asset = await repository.GetForUpdateAsync(assetId);

        Assert.NotNull(asset);
        asset!.AssetName = "Updated Stair";
        await context.SaveChangesAsync();

        var reloaded = await context.LayoutAssetSet.SingleAsync(entity => entity.LayoutAssetId == assetId);
        Assert.Equal("Updated Stair", reloaded.AssetName);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<Guid> SeedAsync(AppDbContext context)
    {
        var activeStairId = Guid.NewGuid();
        context.LayoutAssetSet.AddRange(
            new LayoutAsset
            {
                LayoutAssetId = activeStairId,
                AssetCode = "STAIR-001",
                AssetName = "Straight Stair",
                AssetType = LayoutAssetType.STAIR,
                Status = LayoutAssetStatus.ACTIVE,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new LayoutAsset
            {
                LayoutAssetId = Guid.NewGuid(),
                AssetCode = "DOOR-001",
                AssetName = "Glass Door",
                AssetType = LayoutAssetType.DOOR,
                Status = LayoutAssetStatus.ACTIVE,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new LayoutAsset
            {
                LayoutAssetId = Guid.NewGuid(),
                AssetCode = "STAIR-OLD",
                AssetName = "Old Stair",
                AssetType = LayoutAssetType.STAIR,
                Status = LayoutAssetStatus.INACTIVE,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        await context.SaveChangesAsync();
        return activeStairId;
    }
}
