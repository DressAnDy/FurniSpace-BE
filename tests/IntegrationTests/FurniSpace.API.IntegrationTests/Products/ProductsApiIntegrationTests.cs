using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FurniSpace.API.IntegrationTests.Fixtures;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Products;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;

namespace FurniSpace.API.IntegrationTests.Products;

[Collection(ApiIntegrationCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "Core")]
public sealed class ProductsApiIntegrationTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private readonly ApiIntegrationFixture _fixture;

    public ProductsApiIntegrationTests(ApiIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.Database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetAll_ReturnsDatabaseBackedCatalog()
    {
        var categoryId = await SeedProductsAsync(
            ("Desk", "DESK-001", new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)),
            ("Chair", "CHAIR-001", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

        var response = await _fixture.Client.GetAsync("/products?page=1&limit=10");
        var result = await response.Content.ReadFromJsonAsync<ServiceResult<ProductListResponseDto>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result?.Data);
        Assert.Equal(2, result.Data.Total);
        Assert.Equal(["Desk", "Chair"], result.Data.Items.Select(item => item.ProductName).ToArray());
        Assert.All(result.Data.Items, item => Assert.Equal(categoryId, item.CategoryId));
    }

    [Fact]
    public async Task GetById_ReturnsProductAndCategoryDetail()
    {
        var categoryId = await SeedProductsAsync(
            ("Desk", "DESK-001", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        Guid productId;
        await using (var context = _fixture.Database.CreateDbContext())
        {
            productId = context.ProductSet.Single().ProductId;
        }

        var response = await _fixture.Client.GetAsync($"/products/{productId}");
        var result = await response.Content.ReadFromJsonAsync<ServiceResult<ProductDetailDto>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result?.Data);
        Assert.Equal(categoryId, result.Data.CategoryId);
        Assert.Equal("Office", result.Data.CategoryName);
        Assert.Equal("Desk", result.Data.ProductName);
        Assert.Equal("DESK-001", result.Data.ProductCode);
    }

    [Fact]
    public async Task GetById_WhenProductDoesNotExist_ReturnsNotFound()
    {
        var response = await _fixture.Client.GetAsync($"/products/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<Guid> SeedProductsAsync(params (string Name, string Code, DateTime CreatedAt)[] products)
    {
        var categoryId = Guid.NewGuid();
        await using var context = _fixture.Database.CreateDbContext();
        context.CategorySet.Add(new Category
        {
            CategoryId = categoryId,
            CategoryName = "Office",
            Status = ProductStatus.ACTIVE
        });
        context.ProductSet.AddRange(products.Select(product => new Product
        {
            ProductId = Guid.NewGuid(),
            CategoryId = categoryId,
            ProductCode = product.Code,
            ProductName = product.Name,
            Status = ProductStatus.ACTIVE,
            CreatedAt = product.CreatedAt
        }));
        await context.SaveChangesAsync();
        return categoryId;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
