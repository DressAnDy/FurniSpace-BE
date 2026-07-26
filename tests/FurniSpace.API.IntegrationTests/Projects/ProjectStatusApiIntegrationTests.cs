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
public sealed class ProjectStatusApiIntegrationTests : IAsyncLifetime
{
    private readonly ApiIntegrationFixture _fixture;

    public ProjectStatusApiIntegrationTests(ApiIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.Database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task UpdateStatus_AsAssignedSales_MovesToWaitingForDesignerAssignment()
    {
        ProjectConsultationScenario scenario;
        await using (var context = _fixture.Database.CreateDbContext())
        {
            scenario = await ProjectScenarioSeeder.SeedInConsultationAsync(context);
        }

        using var request = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Patch,
            $"/projects/{scenario.ProjectId}/status",
            scenario.SalesAccountId,
            CoreRoles.Sales,
            new UpdateProjectStatusRequestDto
            {
                Status = ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT,
                Note = "Ready for designer assignment"
            });

        var response = await _fixture.Client.SendAsync(request);
        var result = await response.Content
            .ReadFromJsonAsync<ServiceResult<ProjectStatusUpdateDto>>(IntegrationHttp.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT, result?.Data?.Status);
        Assert.Equal(ProjectStatus.IN_CONSULTATION, result?.Data?.OldStatus);

        await using var verification = _fixture.Database.CreateDbContext();
        var project = await verification.ProjectSet.SingleAsync();
        Assert.Equal(ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT, project.Status);
    }

    [Fact]
    public async Task GetById_WhenProjectDoesNotExist_ReturnsNotFound()
    {
        SeededAccount customer;
        await using (var context = _fixture.Database.CreateDbContext())
        {
            customer = await CoreAccountSeeder.SeedAccountAsync(context, CoreRoles.Customer);
        }

        using var request = IntegrationHttp.Authenticated(
            HttpMethod.Get,
            $"/projects/{Guid.NewGuid()}",
            customer.AccountId,
            CoreRoles.Customer);

        var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
