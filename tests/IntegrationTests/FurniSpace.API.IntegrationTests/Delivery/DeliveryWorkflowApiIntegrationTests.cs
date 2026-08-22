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
    public async Task DeliveryWorkflow_WhenStaffCompletesAndCustomerConfirms_MovesOrderAndProjectDelivered()
    {
        var scenario = await SeedScenarioAsync();
        var schedule = await CreateDeliveryScheduleAsync(scenario, "Morning delivery");

        await ConfirmScheduleAsync(scenario, schedule.ScheduleId);
        await StartDeliveryAsync(scenario);

        var completion = await CompleteDeliveryAsync(scenario);
        Assert.Equal(nameof(OrderStatus.DELIVERING), completion.OrderStatus);
        Assert.Equal(2, completion.DeliveredItemCount);

        var confirmation = await ConfirmDeliveryAsync(scenario);
        Assert.Equal(nameof(OrderStatus.FINAL_PAYMENT_PENDING), confirmation.OrderStatus);
        Assert.Equal(nameof(ProjectStatus.DELIVERED), confirmation.ProjectStatus);
        Assert.NotNull(confirmation.CustomerConfirmedDeliveryAt);

        await using var verification = _fixture.Database.CreateDbContext();
        var order = await verification.OrderSet.FindAsync(scenario.OrderId);
        var project = await verification.ProjectSet.FindAsync(scenario.ProjectId);
        var firstItem = await verification.OrderItemSet.FindAsync(scenario.FirstOrderItemId);
        var secondItem = await verification.OrderItemSet.FindAsync(scenario.SecondOrderItemId);
        var pendingItem = await verification.OrderItemSet.FindAsync(scenario.PendingOrderItemId);

        Assert.Equal(OrderStatus.FINAL_PAYMENT_PENDING, order?.Status);
        Assert.NotNull(order?.CustomerConfirmedDeliveryAt);
        Assert.Equal(ProjectStatus.DELIVERED, project?.Status);
        Assert.Equal(OrderItemStatus.DELIVERED, firstItem?.Status);
        Assert.Equal(OrderItemStatus.DELIVERED, secondItem?.Status);
        Assert.NotNull(firstItem?.DeliveredAt);
        Assert.NotNull(secondItem?.DeliveredAt);
        Assert.Equal(OrderItemStatus.PENDING, pendingItem?.Status);
        Assert.Equal(
            1,
            await verification.PaymentSet.CountAsync(payment =>
                payment.OrderId == scenario.OrderId &&
                payment.PaymentType == PaymentType.REMAINING_PAYMENT &&
                payment.Status == PaymentStatus.PENDING));
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
    public async Task ConfirmDelivery_WhenItemsNotDelivered_ReturnsBadRequest()
    {
        var scenario = await SeedScenarioAsync();
        var schedule = await CreateDeliveryScheduleAsync(scenario, "Incomplete delivery");

        await ConfirmScheduleAsync(scenario, schedule.ScheduleId);
        await StartDeliveryAsync(scenario);

        using var request = IntegrationHttp.Authenticated(
            HttpMethod.Patch,
            $"/orders/{scenario.OrderId}/confirm-delivery",
            scenario.CustomerAccountId,
            CoreRoles.Customer);

        var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
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

    private async Task AdvanceScheduleStartToPastAsync(Guid scheduleId)
    {
        await using var context = _fixture.Database.CreateDbContext();
        var schedule = await context.ProjectScheduleSet.FindAsync(scheduleId);
        Assert.NotNull(schedule);
        schedule!.ScheduledStart = DateTime.UtcNow.AddHours(-2);
        schedule.ScheduledEnd = DateTime.UtcNow.AddHours(-1);
        await context.SaveChangesAsync();
    }

    private async Task CompleteScheduleAsync(DeliveryOrderScenario scenario, Guid scheduleId)
    {
        await AdvanceScheduleStartToPastAsync(scheduleId);
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

    private async Task<OrderDeliveryCompletionDto> CompleteDeliveryAsync(DeliveryOrderScenario scenario)
    {
        using var request = IntegrationHttp.Authenticated(
            HttpMethod.Patch,
            $"/orders/{scenario.OrderId}/complete-delivery",
            scenario.SalesAccountId,
            CoreRoles.Sales);

        var response = await _fixture.Client.SendAsync(request);
        return await ReadDataAsync<OrderDeliveryCompletionDto>(response, HttpStatusCode.OK);
    }

    private async Task<OrderDeliveryConfirmationDto> ConfirmDeliveryAsync(DeliveryOrderScenario scenario)
    {
        using var request = IntegrationHttp.Authenticated(
            HttpMethod.Patch,
            $"/orders/{scenario.OrderId}/confirm-delivery",
            scenario.CustomerAccountId,
            CoreRoles.Customer);

        var response = await _fixture.Client.SendAsync(request);
        return await ReadDataAsync<OrderDeliveryConfirmationDto>(response, HttpStatusCode.OK);
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
