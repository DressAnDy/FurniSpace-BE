using System.Net;
using System.Net.Http.Json;
using FurniSpace.API.IntegrationTests.Fixtures;
using FurniSpace.API.IntegrationTests.Support;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.ProjectSchedules;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Testing.Seeding;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.API.IntegrationTests.ProjectSchedules;

[Collection(ApiIntegrationCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "Core")]
public sealed class ProductionScheduleAccessApiIntegrationTests : IAsyncLifetime
{
    private readonly ApiIntegrationFixture _fixture;

    public ProductionScheduleAccessApiIntegrationTests(ApiIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.Database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ProductionAssignedToActiveRequest_CanListProjectSchedules()
    {
        var scenario = await SeedScheduleAccessScenarioAsync(ProductionRequestStatus.IN_PRODUCTION);

        using var request = IntegrationHttp.Authenticated(
            HttpMethod.Get,
            $"/project-schedules?projectId={scenario.ProjectId}",
            scenario.ProductionAccountId,
            CoreRoles.Production);

        var response = await _fixture.Client.SendAsync(request);
        var list = await ReadDataAsync<ProjectScheduleListResponseDto>(response, HttpStatusCode.OK);

        Assert.Single(list.Items);
        Assert.Equal(scenario.ScheduleId, list.Items[0].ScheduleId);
    }

    [Fact]
    public async Task ProductionAssignedToActiveRequest_CanViewScheduleDetail()
    {
        var scenario = await SeedScheduleAccessScenarioAsync(ProductionRequestStatus.FEASIBLE);

        using var request = IntegrationHttp.Authenticated(
            HttpMethod.Get,
            $"/project-schedules/{scenario.ScheduleId}",
            scenario.ProductionAccountId,
            CoreRoles.Production);

        var response = await _fixture.Client.SendAsync(request);
        var schedule = await ReadDataAsync<ProjectScheduleDto>(response, HttpStatusCode.OK);

        Assert.Equal(scenario.ScheduleId, schedule.ScheduleId);
    }

    [Fact]
    public async Task ProductionWithoutScheduleOrProductionRequestAssignment_GetsForbidden()
    {
        var scenario = await SeedScheduleAccessScenarioAsync(productionRequestStatus: null);

        using var request = IntegrationHttp.Authenticated(
            HttpMethod.Get,
            $"/project-schedules?projectId={scenario.ProjectId}",
            scenario.ProductionAccountId,
            CoreRoles.Production);

        var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ProductionDirectlyAssignedToSchedule_StillCanViewSchedules()
    {
        var scenario = await SeedScheduleAccessScenarioAsync(
            productionRequestStatus: null,
            directlyAssignSchedule: true);

        using var request = IntegrationHttp.Authenticated(
            HttpMethod.Get,
            $"/project-schedules?projectId={scenario.ProjectId}",
            scenario.ProductionAccountId,
            CoreRoles.Production);

        var response = await _fixture.Client.SendAsync(request);

        await ReadDataAsync<ProjectScheduleListResponseDto>(response, HttpStatusCode.OK);
    }

    [Fact]
    public async Task ProductionRequestFromAnotherProject_DoesNotGrantScheduleAccess()
    {
        var scenario = await SeedScheduleAccessScenarioAsync(
            ProductionRequestStatus.IN_PRODUCTION,
            productionRequestOnAnotherProject: true);

        using var request = IntegrationHttp.Authenticated(
            HttpMethod.Get,
            $"/project-schedules/{scenario.ScheduleId}",
            scenario.ProductionAccountId,
            CoreRoles.Production);

        var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CancelledProductionRequest_DoesNotGrantScheduleAccess()
    {
        var scenario = await SeedScheduleAccessScenarioAsync(ProductionRequestStatus.CANCELLED);

        using var request = IntegrationHttp.Authenticated(
            HttpMethod.Get,
            $"/project-schedules?projectId={scenario.ProjectId}",
            scenario.ProductionAccountId,
            CoreRoles.Production);

        var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CompletedProductionRequest_GrantsHistoryScheduleAccess()
    {
        var scenario = await SeedScheduleAccessScenarioAsync(ProductionRequestStatus.COMPLETED);

        using var request = IntegrationHttp.Authenticated(
            HttpMethod.Get,
            $"/project-schedules/{scenario.ScheduleId}",
            scenario.ProductionAccountId,
            CoreRoles.Production);

        var response = await _fixture.Client.SendAsync(request);

        await ReadDataAsync<ProjectScheduleDto>(response, HttpStatusCode.OK);
    }

    [Fact]
    public async Task ExistingParticipantRules_RemainAllowed()
    {
        var scenario = await SeedScheduleAccessScenarioAsync(
            ProductionRequestStatus.IN_PRODUCTION,
            assignDesigner: true);

        await AssertScheduleListAllowedAsync(scenario.CustomerAccountId, CoreRoles.Customer, scenario.ProjectId);
        await AssertScheduleListAllowedAsync(scenario.SalesAccountId, CoreRoles.Sales, scenario.ProjectId);
        await AssertScheduleListAllowedAsync(scenario.DesignerAccountId!.Value, CoreRoles.Designer, scenario.ProjectId);
    }

    [Fact]
    public async Task ProductionRequestReadAccess_DoesNotAllowScheduleMutation()
    {
        var scenario = await SeedScheduleAccessScenarioAsync(ProductionRequestStatus.IN_PRODUCTION);

        using var request = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Patch,
            $"/project-schedules/{scenario.ScheduleId}",
            scenario.ProductionAccountId,
            CoreRoles.Production,
            new UpdateProjectScheduleRequestDto { Title = "Unauthorized update" });

        var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_RemainsUnrestricted()
    {
        var scenario = await SeedScheduleAccessScenarioAsync(productionRequestStatus: null);
        var admin = await SeedAdminAsync();

        using var request = IntegrationHttp.Authenticated(
            HttpMethod.Get,
            $"/project-schedules/{scenario.ScheduleId}",
            admin.AccountId,
            CoreRoles.Admin);

        var response = await _fixture.Client.SendAsync(request);

        await ReadDataAsync<ProjectScheduleDto>(response, HttpStatusCode.OK);
    }

    private async Task AssertScheduleListAllowedAsync(Guid userId, string role, Guid projectId)
    {
        using var request = IntegrationHttp.Authenticated(
            HttpMethod.Get,
            $"/project-schedules?projectId={projectId}",
            userId,
            role);

        var response = await _fixture.Client.SendAsync(request);
        await ReadDataAsync<ProjectScheduleListResponseDto>(response, HttpStatusCode.OK);
    }

    private async Task<ProductionScheduleAccessScenario> SeedScheduleAccessScenarioAsync(
        ProductionRequestStatus? productionRequestStatus,
        bool directlyAssignSchedule = false,
        bool productionRequestOnAnotherProject = false,
        bool assignDesigner = false)
    {
        await using var context = _fixture.Database.CreateDbContext();
        var delivery = await DeliveryScenarioSeeder.SeedReadyForDeliveryOrderAsync(context);
        var otherProduction = await CoreAccountSeeder.SeedAccountAsync(context, CoreRoles.Production);
        var designer = assignDesigner
            ? await CoreAccountSeeder.SeedAccountAsync(context, CoreRoles.Designer)
            : null;

        var project = await context.ProjectSet.FindAsync(delivery.ProjectId)
            ?? throw new InvalidOperationException("Seeded project was not found.");
        if (designer is not null)
        {
            project.AssignedDesignerId = designer.AccountId;
        }

        var schedule = new ProjectSchedule
        {
            ScheduleId = Guid.NewGuid(),
            ProjectId = delivery.ProjectId,
            ScheduleType = ProjectScheduleType.DELIVERY,
            Title = "Production-visible delivery",
            AssignedStaffId = directlyAssignSchedule ? delivery.ProductionAccountId : otherProduction.AccountId,
            ScheduledStart = DateTime.UtcNow.AddDays(2),
            Status = ProjectScheduleStatus.CONFIRMED,
            CreatedAt = CoreAccountSeeder.FixedTimestamp
        };
        context.ProjectScheduleSet.Add(schedule);

        if (productionRequestStatus.HasValue)
        {
            var requestProjectId = productionRequestOnAnotherProject
                ? await SeedOtherProjectAsync(context, delivery.CustomerAccountId, delivery.SalesAccountId)
                : delivery.ProjectId;
            context.ProductionRequestSet.Add(CreateProductionRequest(
                requestProjectId,
                delivery.OrderId,
                delivery.ProductionAccountId,
                productionRequestStatus.Value));
        }

        await context.SaveChangesAsync();

        return new ProductionScheduleAccessScenario(
            delivery.ProjectId,
            schedule.ScheduleId,
            delivery.CustomerAccountId,
            delivery.SalesAccountId,
            delivery.ProductionAccountId,
            designer?.AccountId);
    }

    private async Task<SeededAccount> SeedAdminAsync()
    {
        await using var context = _fixture.Database.CreateDbContext();
        return await CoreAccountSeeder.SeedAccountAsync(context, CoreRoles.Admin);
    }

    private static async Task<Guid> SeedOtherProjectAsync(
        FurniSpace.Infrastructure.Data.AppDbContext context,
        Guid customerId,
        Guid salesId)
    {
        var project = ProjectScenarioSeeder.CreateProject(
            customerId,
            salesId,
            $"PRJ-OTHER-{Guid.NewGuid():N}"[..18],
            "Other Project",
            ProjectStatus.IN_PRODUCTION);
        context.ProjectSet.Add(project);
        await context.SaveChangesAsync();
        return project.ProjectId;
    }

    private static ProductionRequest CreateProductionRequest(
        Guid projectId,
        Guid orderId,
        Guid productionId,
        ProductionRequestStatus status)
    {
        return new ProductionRequest
        {
            ProductionRequestId = Guid.NewGuid(),
            ProjectId = projectId,
            OrderId = orderId,
            AssignedTo = productionId,
            ProductionCode = $"PRD-SCH-{Guid.NewGuid():N}"[..18],
            Status = status,
            CreatedAt = CoreAccountSeeder.FixedTimestamp
        };
    }

    private static async Task<T> ReadDataAsync<T>(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus)
        where T : class
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ServiceResult<T>>(IntegrationHttp.JsonOptions);
        Assert.NotNull(result?.Data);
        return result.Data;
    }

    private sealed record ProductionScheduleAccessScenario(
        Guid ProjectId,
        Guid ScheduleId,
        Guid CustomerAccountId,
        Guid SalesAccountId,
        Guid ProductionAccountId,
        Guid? DesignerAccountId);
}
