using System.Net;
using System.Net.Http.Json;
using FurniSpace.API.IntegrationTests.Fixtures;
using FurniSpace.API.IntegrationTests.Support;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.ProjectAreas;
using FurniSpace.Domain.Enums;
using FurniSpace.Testing.Seeding;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.API.IntegrationTests.ProjectAreas;

[Collection(ApiIntegrationCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "Core")]
public sealed class ProjectAreasApiIntegrationTests : IAsyncLifetime
{
    private readonly ApiIntegrationFixture _fixture;

    public ProjectAreasApiIntegrationTests(ApiIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.Database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Create_NestedAreaHierarchy_PersistsParentChild()
    {
        MeasurementScenario scenario;
        await using (var context = _fixture.Database.CreateDbContext())
        {
            scenario = await MeasurementScenarioSeeder.SeedMeasurementRequiredAsync(context);
        }

        using var floorRequest = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Post,
            $"/projects/{scenario.ProjectId}/areas",
            scenario.SalesAccountId,
            CoreRoles.Sales,
            new CreateProjectAreaRequestDto
            {
                AreaName = "Ground Floor",
                AreaType = ProjectAreaType.FLOOR,
                FloorNumber = 1,
                AreaSqm = 48m,
                Width = 8m,
                Length = 6m,
                Height = 3.2m
            });
        var floorResponse = await _fixture.Client.SendAsync(floorRequest);
        var floor = await floorResponse.Content
            .ReadFromJsonAsync<ServiceResult<ProjectAreaDto>>(IntegrationHttp.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, floorResponse.StatusCode);
        Assert.NotNull(floor?.Data);

        using var zoneRequest = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Post,
            $"/projects/{scenario.ProjectId}/areas",
            scenario.SalesAccountId,
            CoreRoles.Sales,
            new CreateProjectAreaRequestDto
            {
                AreaName = "Reception Zone",
                AreaType = ProjectAreaType.ZONE,
                ParentAreaId = floor.Data.ProjectAreaId,
                AreaSqm = 12m,
                Width = 4m,
                Length = 3m,
                Height = 3.2m
            });
        var zoneResponse = await _fixture.Client.SendAsync(zoneRequest);
        var zone = await zoneResponse.Content
            .ReadFromJsonAsync<ServiceResult<ProjectAreaDto>>(IntegrationHttp.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, zoneResponse.StatusCode);
        Assert.Equal(floor.Data.ProjectAreaId, zone?.Data?.ParentAreaId);

        await using var verification = _fixture.Database.CreateDbContext();
        Assert.Equal(2, await verification.ProjectAreaSet.CountAsync());
        var child = await verification.ProjectAreaSet.SingleAsync(a => a.AreaName == "Reception Zone");
        Assert.Equal(floor.Data.ProjectAreaId, child.ParentAreaId);
    }

    [Fact]
    public async Task Create_WithParentFromDifferentProject_ReturnsBadRequest()
    {
        MeasurementScenario scenario;
        Guid foreignParentId;
        await using (var context = _fixture.Database.CreateDbContext())
        {
            scenario = await MeasurementScenarioSeeder.SeedMeasurementRequiredAsync(context);
            var other = await MeasurementScenarioSeeder.SeedMeasurementRequiredAsync(context);
            var foreignParent = MeasurementScenarioSeeder.CreateArea(
                other.ProjectId,
                "Foreign Floor",
                ProjectAreaType.FLOOR,
                floorNumber: 1);
            foreignParentId = foreignParent.ProjectAreaId;
            context.ProjectAreaSet.Add(foreignParent);
            await context.SaveChangesAsync();
        }

        using var request = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Post,
            $"/projects/{scenario.ProjectId}/areas",
            scenario.SalesAccountId,
            CoreRoles.Sales,
            new CreateProjectAreaRequestDto
            {
                AreaName = "Invalid Child",
                AreaType = ProjectAreaType.ZONE,
                ParentAreaId = foreignParentId
            });

        var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var verification = _fixture.Database.CreateDbContext();
        Assert.Equal(1, await verification.ProjectAreaSet.CountAsync());
    }
}
