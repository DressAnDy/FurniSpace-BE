using System.Net;
using System.Net.Http.Json;
using FurniSpace.API.IntegrationTests.Fixtures;
using FurniSpace.API.IntegrationTests.Support;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Orders;
using FurniSpace.Application.DTOs.ProjectSchedules;
using FurniSpace.Domain.Enums;
using FurniSpace.Testing.Seeding;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.API.IntegrationTests.Delivery;

[Collection(ApiIntegrationCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "Core")]
public sealed class DeliveryWorkflowApiIntegrationTests : IAsyncLifetime
{
    private readonly ApiIntegrationFixture _fixture;

    public DeliveryWorkflowApiIntegrationTests(ApiIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.Database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task DeliveryWorkflow_WhenCustomerConfirmsAllItems_MovesOrderAndProjectDelivered()
    {
        var scenario = await SeedScenarioAsync();
        var schedule = await CreateDeliveryScheduleAsync(scenario, "Morning delivery");

        await ConfirmScheduleAsync(scenario, schedule.ScheduleId);
        await StartDeliveryAsync(scenario);
        await CompleteScheduleAsync(scenario, schedule.ScheduleId);

        await UpdateDeliveredQuantityAsync(scenario, scenario.FirstOrderItemId, increment: 2);
        var firstConfirmation = await ConfirmDeliveryAsync(scenario, scenario.FirstOrderItemId);

        Assert.Equal(nameof(OrderItemStatus.DELIVERED), firstConfirmation.Status);
        Assert.Equal(nameof(OrderStatus.DELIVERING), firstConfirmation.OrderStatus);

        await UpdateDeliveredQuantityAsync(scenario, scenario.SecondOrderItemId, increment: 1);
        var finalConfirmation = await ConfirmDeliveryAsync(scenario, scenario.SecondOrderItemId);

        Assert.Equal(nameof(OrderItemStatus.DELIVERED), finalConfirmation.Status);
        Assert.Equal(nameof(OrderStatus.DELIVERED), finalConfirmation.OrderStatus);

        await using var verification = _fixture.Database.CreateDbContext();
        var order = await verification.OrderSet.FindAsync(scenario.OrderId);
        var project = await verification.ProjectSet.FindAsync(scenario.ProjectId);
        var manualItem = await verification.OrderItemSet.FindAsync(scenario.ManualOrderItemId);
        var completedSchedule = await verification.ProjectScheduleSet.FindAsync(schedule.ScheduleId);

        Assert.Equal(OrderStatus.DELIVERED, order?.Status);
        Assert.NotNull(order?.CustomerConfirmedDeliveryAt);
        Assert.Equal(ProjectStatus.DELIVERED, project?.Status);
        Assert.Equal(OrderItemStatus.PENDING, manualItem?.Status);
        Assert.Equal(ProjectScheduleStatus.COMPLETED, completedSchedule?.Status);
    }

    [Fact]
    public async Task StartDelivery_WhenNoConfirmedDeliverySchedule_ReturnsBadRequest()
    {
        var scenario = await SeedScenarioAsync();
        await CreateDeliveryScheduleAsync(scenario, "Unconfirmed delivery");

        using var request = IntegrationHttp.Authenticated(
            HttpMethod.Patch,
            $"/orders/{scenario.OrderId}/start-delivery",
            scenario.SalesAccountId,
            CoreRoles.Sales);

        var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var verification = _fixture.Database.CreateDbContext();
        var order = await verification.OrderSet.FindAsync(scenario.OrderId);
        Assert.Equal(OrderStatus.READY_FOR_DELIVERY, order?.Status);
    }

    [Fact]
    public async Task UpdateDeliveredQuantity_WhenIncrementExceedsQuantity_ReturnsBadRequest()
    {
        var scenario = await SeedScenarioAsync();
        var schedule = await CreateDeliveryScheduleAsync(scenario, "Quantity guard delivery");

        await ConfirmScheduleAsync(scenario, schedule.ScheduleId);
        await StartDeliveryAsync(scenario);

        using var request = BuildDeliveredQuantityRequest(
            scenario,
            scenario.SecondOrderItemId,
            increment: 2);

        var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var verification = _fixture.Database.CreateDbContext();
        var item = await verification.OrderItemSet.FindAsync(scenario.SecondOrderItemId);
        Assert.Equal(0, item?.DeliveredQuantity);
    }

    [Fact]
    public async Task UpdateDeliveredQuantity_WhenConcurrentIncrements_DoNotLoseUpdates()
    {
        var scenario = await SeedScenarioAsync();
        var schedule = await CreateDeliveryScheduleAsync(scenario, "Concurrent delivery");

        await ConfirmScheduleAsync(scenario, schedule.ScheduleId);
        await StartDeliveryAsync(scenario);

        var responses = await Task.WhenAll(
            UpdateDeliveredQuantityResponseAsync(scenario, scenario.FirstOrderItemId, increment: 1),
            UpdateDeliveredQuantityResponseAsync(scenario, scenario.FirstOrderItemId, increment: 1));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        foreach (var response in responses)
        {
            response.Dispose();
        }

        await using var verification = _fixture.Database.CreateDbContext();
        var item = await verification.OrderItemSet.FindAsync(scenario.FirstOrderItemId);
        Assert.Equal(2, item?.DeliveredQuantity);
    }

    [Fact]
    public async Task ConfirmDelivery_WhenItemIsNotFullyDelivered_ReturnsBadRequest()
    {
        var scenario = await SeedScenarioAsync();
        var schedule = await CreateDeliveryScheduleAsync(scenario, "Partial delivery");

        await ConfirmScheduleAsync(scenario, schedule.ScheduleId);
        await StartDeliveryAsync(scenario);
        await UpdateDeliveredQuantityAsync(scenario, scenario.FirstOrderItemId, increment: 1);

        using var request = IntegrationHttp.Authenticated(
            HttpMethod.Patch,
            $"/order-items/{scenario.FirstOrderItemId}/confirm-delivery",
            scenario.CustomerAccountId,
            CoreRoles.Customer);

        var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CompletingDeliveryScheduleAlone_DoesNotMarkOrderDelivered()
    {
        var scenario = await SeedScenarioAsync();
        var schedule = await CreateDeliveryScheduleAsync(scenario, "Schedule only delivery");

        await ConfirmScheduleAsync(scenario, schedule.ScheduleId);
        await StartDeliveryAsync(scenario);
        await CompleteScheduleAsync(scenario, schedule.ScheduleId);

        await using var verification = _fixture.Database.CreateDbContext();
        var order = await verification.OrderSet.FindAsync(scenario.OrderId);
        var project = await verification.ProjectSet.FindAsync(scenario.ProjectId);

        Assert.Equal(OrderStatus.DELIVERING, order?.Status);
        Assert.Equal(ProjectStatus.DELIVERING, project?.Status);
    }

    [Fact]
    public async Task CreateDeliverySchedule_AllowsMultipleSchedules()
    {
        var scenario = await SeedScenarioAsync();

        var first = await CreateDeliveryScheduleAsync(scenario, "First delivery");
        var second = await CreateDeliveryScheduleAsync(scenario, "Second delivery");

        Assert.NotEqual(first.ScheduleId, second.ScheduleId);
        await using var verification = _fixture.Database.CreateDbContext();
        Assert.Equal(2, await verification.ProjectScheduleSet.CountAsync());
    }

    private async Task<DeliveryOrderScenario> SeedScenarioAsync()
    {
        await using var context = _fixture.Database.CreateDbContext();
        return await DeliveryScenarioSeeder.SeedReadyForDeliveryOrderAsync(context);
    }

    private async Task<ProjectScheduleDto> CreateDeliveryScheduleAsync(
        DeliveryOrderScenario scenario,
        string title)
    {
        using var request = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Post,
            $"/projects/{scenario.ProjectId}/schedules",
            scenario.SalesAccountId,
            CoreRoles.Sales,
            new CreateProjectScheduleRequestDto
            {
                ScheduleType = ProjectScheduleType.DELIVERY,
                Title = title,
                Description = "Delivery appointment",
                AssignedStaffId = scenario.ProductionAccountId,
                ScheduledStart = DateTime.UtcNow.AddDays(1),
                ScheduledEnd = DateTime.UtcNow.AddDays(1).AddHours(2),
                Location = "Customer site"
            });

        var response = await _fixture.Client.SendAsync(request);
        var schedule = await ReadDataAsync<ProjectScheduleDto>(response, HttpStatusCode.Created);
        Assert.Equal(ProjectScheduleStatus.PENDING_CONFIRMATION, schedule.Status);
        return schedule;
    }

    private async Task ConfirmScheduleAsync(DeliveryOrderScenario scenario, Guid scheduleId)
    {
        await UpdateScheduleStatusAsync(
            scenario.CustomerAccountId,
            CoreRoles.Customer,
            scheduleId,
            ProjectScheduleStatus.CONFIRMED);
    }

    private async Task CompleteScheduleAsync(DeliveryOrderScenario scenario, Guid scheduleId)
    {
        await UpdateScheduleStatusAsync(
            scenario.ProductionAccountId,
            CoreRoles.Production,
            scheduleId,
            ProjectScheduleStatus.COMPLETED);
    }

    private async Task UpdateScheduleStatusAsync(
        Guid userId,
        string role,
        Guid scheduleId,
        ProjectScheduleStatus status)
    {
        using var request = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Patch,
            $"/project-schedules/{scheduleId}/status",
            userId,
            role,
            new UpdateProjectScheduleStatusRequestDto
            {
                Status = status,
                Note = $"{status} delivery schedule"
            });

        var response = await _fixture.Client.SendAsync(request);
        var schedule = await ReadDataAsync<ProjectScheduleDto>(response, HttpStatusCode.OK);
        Assert.Equal(status, schedule.Status);
    }

    private async Task StartDeliveryAsync(DeliveryOrderScenario scenario)
    {
        using var request = IntegrationHttp.Authenticated(
            HttpMethod.Patch,
            $"/orders/{scenario.OrderId}/start-delivery",
            scenario.SalesAccountId,
            CoreRoles.Sales);

        var response = await _fixture.Client.SendAsync(request);
        var delivery = await ReadDataAsync<OrderDeliveryStartDto>(response, HttpStatusCode.OK);
        Assert.Equal(nameof(OrderStatus.DELIVERING), delivery.OrderStatus);
        Assert.Equal(nameof(ProjectStatus.DELIVERING), delivery.ProjectStatus);
    }

    private async Task UpdateDeliveredQuantityAsync(
        DeliveryOrderScenario scenario,
        Guid orderItemId,
        int increment)
    {
        using var response = await UpdateDeliveredQuantityResponseAsync(scenario, orderItemId, increment);
        var delivery = await ReadDataAsync<OrderItemDeliveredQuantityDto>(response, HttpStatusCode.OK);
        Assert.Equal(orderItemId, delivery.OrderItemId);
    }

    private async Task<HttpResponseMessage> UpdateDeliveredQuantityResponseAsync(
        DeliveryOrderScenario scenario,
        Guid orderItemId,
        int increment)
    {
        using var request = BuildDeliveredQuantityRequest(scenario, orderItemId, increment);
        return await _fixture.Client.SendAsync(request);
    }

    private static HttpRequestMessage BuildDeliveredQuantityRequest(
        DeliveryOrderScenario scenario,
        Guid orderItemId,
        int increment)
    {
        return IntegrationHttp.AuthenticatedJson(
            HttpMethod.Patch,
            $"/order-items/{orderItemId}/delivered-quantity",
            scenario.SalesAccountId,
            CoreRoles.Sales,
            new UpdateDeliveredQuantityRequestDto
            {
                DeliveredQuantityIncrement = increment,
                DeliveryNote = $"Delivered {increment}"
            });
    }

    private async Task<OrderItemDeliveryConfirmationDto> ConfirmDeliveryAsync(
        DeliveryOrderScenario scenario,
        Guid orderItemId)
    {
        using var request = IntegrationHttp.Authenticated(
            HttpMethod.Patch,
            $"/order-items/{orderItemId}/confirm-delivery",
            scenario.CustomerAccountId,
            CoreRoles.Customer);

        var response = await _fixture.Client.SendAsync(request);
        return await ReadDataAsync<OrderItemDeliveryConfirmationDto>(response, HttpStatusCode.OK);
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
}
