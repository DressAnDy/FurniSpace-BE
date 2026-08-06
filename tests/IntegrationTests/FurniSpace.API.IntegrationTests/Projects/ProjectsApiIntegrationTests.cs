using System.Net;
using System.Net.Http.Json;
using FurniSpace.API.IntegrationTests.Fixtures;
using FurniSpace.API.IntegrationTests.Support;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Projects;
using FurniSpace.Domain.Enums;
using FurniSpace.Testing.Seeding;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.API.IntegrationTests.Projects;

[Collection(ApiIntegrationCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "Core")]
public sealed class ProjectsApiIntegrationTests : IAsyncLifetime
{
    private readonly ApiIntegrationFixture _fixture;

    public ProjectsApiIntegrationTests(ApiIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.Database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Customer_CreateThenGet_PersistsProjectThroughHttpPipeline()
    {
        SeededAccount customer;
        await using (var context = _fixture.Database.CreateDbContext())
        {
            customer = await CoreAccountSeeder.SeedAccountAsync(
                context,
                CoreRoles.Customer,
                "customer@integration.test");
        }

        using var createRequest = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Post,
            "/projects",
            customer.AccountId,
            CoreRoles.Customer,
            new CreateProjectRequestDto
            {
                ProjectName = "  New Office  ",
                BusinessType = "  Office  ",
                FurnitureRequirement = "  Desks and chairs  ",
                BudgetMin = 100_000_000,
                BudgetMax = 200_000_000
            });

        var createResponse = await _fixture.Client.SendAsync(createRequest);
        var created = await createResponse.Content
            .ReadFromJsonAsync<ServiceResult<ProjectDto>>(IntegrationHttp.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(created?.Data);
        Assert.Equal(ProjectStatus.SUBMITTED, created.Data.Status);

        using var getRequest = IntegrationHttp.Authenticated(
            HttpMethod.Get,
            $"/projects/{created.Data.ProjectId}",
            customer.AccountId,
            CoreRoles.Customer);
        var getResponse = await _fixture.Client.SendAsync(getRequest);
        var detail = await getResponse.Content
            .ReadFromJsonAsync<ServiceResult<ProjectDto>>(IntegrationHttp.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal("New Office", detail?.Data?.ProjectName);

        await using var verification = _fixture.Database.CreateDbContext();
        var persisted = await verification.ProjectSet.SingleAsync();
        Assert.Equal(customer.AccountId, persisted.CustomerId);
        Assert.Equal("Desks and chairs", persisted.FurnitureRequirement);
    }

    [Fact]
    public async Task GetById_ForDifferentCustomer_ReturnsForbidden()
    {
        SeededAccount otherCustomer;
        Guid projectId;
        await using (var context = _fixture.Database.CreateDbContext())
        {
            var owner = await CoreAccountSeeder.SeedAccountAsync(
                context,
                CoreRoles.Customer,
                "owner@integration.test");
            otherCustomer = await CoreAccountSeeder.SeedAccountAsync(
                context,
                CoreRoles.Customer,
                "other@integration.test");

            var project = ProjectScenarioSeeder.CreateProject(
                owner.AccountId,
                assignedSalesId: null,
                "PRJ-2026-0001",
                "Owner Project",
                ProjectStatus.SUBMITTED);
            projectId = project.ProjectId;
            context.ProjectSet.Add(project);
            await context.SaveChangesAsync();
        }

        using var request = IntegrationHttp.Authenticated(
            HttpMethod.Get,
            $"/projects/{projectId}",
            otherCustomer.AccountId,
            CoreRoles.Customer);
        var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
