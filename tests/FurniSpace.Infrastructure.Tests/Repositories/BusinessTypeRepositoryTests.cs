#nullable enable

using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.Repositories.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.Repositories;

public sealed class BusinessTypeRepositoryTests
{
    [Fact]
    public async Task GetPagedCountAndDetailAsync_ReturnFilteredBusinessTypes()
    {
        await using var context = CreateContext();
        var createdAt = new DateTime(2026, 7, 20, 10, 0, 0, DateTimeKind.Utc);
        context.BusinessTypeSet.AddRange(
            CreateBusinessType(1, "CAFE", "Cafe", true, createdAt),
            CreateBusinessType(2, "SPA", "Spa", true, createdAt.AddMinutes(1)),
            CreateBusinessType(3, "FASHION_STORE", "Fashion Store", false, createdAt.AddMinutes(2)));
        await context.SaveChangesAsync();
        var repository = new BusinessTypeRepository(context);

        var items = await repository.GetPagedAsync(status: true, keyword: null, page: 1, limit: 10);
        var count = await repository.CountAsync(status: true, keyword: null);
        var detail = await repository.GetByIdAsync(2);
        var missing = await repository.GetByIdAsync(99);

        Assert.Equal(2, count);
        Assert.Equal([1, 2], items.Select(item => item.Id));
        Assert.NotNull(detail);
        Assert.Equal("SPA", detail.Code);
        Assert.Null(missing);
    }

    [Fact]
    public async Task AddCodeExistsAndGetForUpdateAsync_UseTrackedBusinessType()
    {
        await using var context = CreateContext();
        var repository = new BusinessTypeRepository(context);
        var businessType = CreateBusinessType(4, "RESTAURANT", "Restaurant", true, DateTime.UtcNow);

        await repository.AddAsync(businessType);
        await context.SaveChangesAsync();
        var exists = await repository.CodeExistsAsync("RESTAURANT");
        var missing = await repository.CodeExistsAsync("CAFE");
        var byIds = await repository.GetByIdsAsync([4, 99]);
        var tracked = await repository.GetForUpdateAsync(4);
        tracked!.Name = "Restaurant Updated";
        await context.SaveChangesAsync();
        var detail = await repository.GetByIdAsync(4);

        Assert.True(exists);
        Assert.False(missing);
        Assert.Single(byIds);
        Assert.Equal(4, byIds[0].Id);
        Assert.Same(businessType, tracked);
        Assert.Equal("Restaurant Updated", detail!.Name);
    }

    [Fact]
    public void BuildSearchPattern_EscapesLikeWildcards()
    {
        var method = typeof(BusinessTypeRepository).GetMethod(
            "BuildSearchPattern",
            BindingFlags.NonPublic | BindingFlags.Static);

        var pattern = method!.Invoke(null, ["  cafe_%  "]);

        Assert.Equal("%cafe\\_\\%%", pattern);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static BusinessType CreateBusinessType(
        int id,
        string code,
        string name,
        bool status,
        DateTime createdAt)
    {
        return new BusinessType
        {
            Id = id,
            Code = code,
            Name = name,
            Status = status,
            CreatedAt = createdAt
        };
    }
}
