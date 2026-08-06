using FurniSpace.Application.DTOs.Categories;
using FurniSpace.Application.IntegrationTests.Fixtures;
using FurniSpace.Application.Services.Categories;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.Repositories.Repository;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Application.IntegrationTests.Categories;

[Collection(ApplicationIntegrationCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "Core")]
public sealed class CategoryServiceIntegrationTests : IAsyncLifetime
{
    private readonly ApplicationIntegrationFixture _fixture;

    public CategoryServiceIntegrationTests(ApplicationIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.Database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateAsync_PersistsNormalizedCategoryThroughUnitOfWork()
    {
        await using var context = _fixture.Database.CreateDbContext();
        var service = CreateService(context);

        var result = await service.CreateAsync(new CreateCategoryRequestDto
        {
            CategoryName = "  Workspace  ",
            Description = "  Desks and office storage  "
        });

        Assert.Equal(201, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal("Workspace", result.Data.CategoryName);
        Assert.Equal("Desks and office storage", result.Data.Description);

        context.ChangeTracker.Clear();
        var persisted = await context.CategorySet.SingleAsync();
        Assert.Equal(result.Data.CategoryId, persisted.CategoryId);
        Assert.Equal("Workspace", persisted.CategoryName);
        Assert.Equal(ProductStatus.ACTIVE, persisted.Status);
    }

    [Fact]
    public async Task CreateAsync_WhenNameDiffersOnlyByCase_ReturnsConflict()
    {
        await using var context = _fixture.Database.CreateDbContext();
        var service = CreateService(context);

        var first = await service.CreateAsync(new CreateCategoryRequestDto
        {
            CategoryName = "Lighting"
        });
        var duplicate = await service.CreateAsync(new CreateCategoryRequestDto
        {
            CategoryName = "lighting"
        });

        Assert.Equal(201, first.Status);
        Assert.Equal(409, duplicate.Status);
        Assert.Equal(1, await context.CategorySet.CountAsync());
    }

    [Fact]
    public async Task GetAllAsync_UsesDatabaseOrderingAndPagination()
    {
        await using var context = _fixture.Database.CreateDbContext();
        context.CategorySet.AddRange(
            CreateCategory("Storage"),
            CreateCategory("Lighting"),
            CreateCategory("Chairs"));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var result = await service.GetAllAsync(page: 1, limit: 2);

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(3, result.Data.Total);
        Assert.Equal(["Chairs", "Lighting"], result.Data.Items.Select(item => item.CategoryName).ToArray());
    }

    private static CategoryService CreateService(AppDbContext context) =>
        new(
            new CategoryRepository(context),
            new UnitOfWork(context));

    private static Category CreateCategory(string name) =>
        new()
        {
            CategoryId = Guid.NewGuid(),
            CategoryName = name,
            Status = ProductStatus.ACTIVE
        };
}
