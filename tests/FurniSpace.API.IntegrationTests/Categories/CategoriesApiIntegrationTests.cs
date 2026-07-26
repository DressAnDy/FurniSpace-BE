using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FurniSpace.API.IntegrationTests.Authentication;
using FurniSpace.API.IntegrationTests.Fixtures;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Categories;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.API.IntegrationTests.Categories;

[Collection(ApiIntegrationCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "Core")]
public sealed class CategoriesApiIntegrationTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private readonly ApiIntegrationFixture _fixture;

    public CategoriesApiIntegrationTests(ApiIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.Database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetAll_ReturnsDatabaseBackedPaginatedResponse()
    {
        await using (var context = _fixture.Database.CreateDbContext())
        {
            context.CategorySet.AddRange(
                CreateCategory("Storage"),
                CreateCategory("Lighting"),
                CreateCategory("Chairs"));
            await context.SaveChangesAsync();
        }

        var response = await _fixture.Client.GetAsync("/categories?page=1&limit=2");
        var result = await response.Content.ReadFromJsonAsync<ServiceResult<CategoryListResponseDto>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result?.Data);
        Assert.Equal(3, result.Data.Total);
        Assert.Equal(["Chairs", "Lighting"], result.Data.Items.Select(item => item.CategoryName).ToArray());
    }

    [Fact]
    public async Task Create_WithoutAuthentication_ReturnsUnauthorized()
    {
        var response = await _fixture.Client.PostAsJsonAsync(
            "/categories",
            new CreateCategoryRequestDto { CategoryName = "Workspace" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithAdminAuthentication_PersistsNormalizedCategory()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/categories")
        {
            Content = JsonContent.Create(new CreateCategoryRequestDto
            {
                CategoryName = "  Workspace  ",
                Description = "  Desks and storage  "
            })
        };
        request.Headers.Add(TestAuthenticationHandler.UserIdHeader, Guid.NewGuid().ToString());
        request.Headers.Add(TestAuthenticationHandler.RoleHeader, "ADMIN");

        var response = await _fixture.Client.SendAsync(request);
        var result = await response.Content.ReadFromJsonAsync<ServiceResult<CategoryDto>>(JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(result?.Data);
        Assert.Equal("Workspace", result.Data.CategoryName);
        Assert.Equal("Desks and storage", result.Data.Description);

        await using var context = _fixture.Database.CreateDbContext();
        var persisted = await context.CategorySet.SingleAsync();
        Assert.Equal(result.Data.CategoryId, persisted.CategoryId);
        Assert.Equal(ProductStatus.ACTIVE, persisted.Status);
    }

    [Fact]
    public async Task Create_WhenNameAlreadyExists_ReturnsConflict()
    {
        await using (var context = _fixture.Database.CreateDbContext())
        {
            context.CategorySet.Add(CreateCategory("Workspace"));
            await context.SaveChangesAsync();
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "/categories")
        {
            Content = JsonContent.Create(new CreateCategoryRequestDto { CategoryName = "workspace" })
        };
        request.Headers.Add(TestAuthenticationHandler.UserIdHeader, Guid.NewGuid().ToString());
        request.Headers.Add(TestAuthenticationHandler.RoleHeader, "ADMIN");

        var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await using var verificationContext = _fixture.Database.CreateDbContext();
        Assert.Equal(1, await verificationContext.CategorySet.CountAsync());
    }

    private static Category CreateCategory(string name) =>
        new()
        {
            CategoryId = Guid.NewGuid(),
            CategoryName = name,
            Status = ProductStatus.ACTIVE
        };

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
