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
    public async Task DeliveryWorkflow_WhenBatchCompletesAndCustomerConfirms_MovesOrderAndProjectDelivered()
    {
        var scenario = await SeedScenarioAsync();
        var schedule = await CreateDeliveryScheduleAsync(scenario, "Morning delivery");

        await ConfirmScheduleAsync(scenario, schedule.ScheduleId);

        var batch = await CreateDeliveryBatchAsync(scenario, schedule.ScheduleId);
        var completion = await CompleteDeliveryBatchAsync(scenario, batch.DeliveryId);
        Assert.Equal(DeliveryStatus.COMPLETED, completion.Status);
        Assert.Equal(2, completion.UpdatedItemCount);

        await using (var afterBatchContext = _fixture.Database.CreateDbContext())
        {
            var orderAfterBatch = await afterBatchContext.OrderSet.FindAsync(scenario.OrderId);
            var firstAfterBatch = await afterBatchContext.OrderItemSet.FindAsync(scenario.FirstOrderItemId);
            var secondAfterBatch = await afterBatchContext.OrderItemSet.FindAsync(scenario.SecondOrderItemId);

            Assert.Equal(OrderStatus.AWAITING_CUSTOMER_CONFIRMATION, orderAfterBatch?.Status);
            Assert.Equal(OrderItemStatus.PHYSICALLY_DELIVERED, firstAfterBatch?.Status);
            Assert.Equal(OrderItemStatus.PHYSICALLY_DELIVERED, secondAfterBatch?.Status);
        }

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
        var linkedSchedule = await verification.ProjectScheduleSet.FindAsync(schedule.ScheduleId);

        Assert.Equal(OrderStatus.FINAL_PAYMENT_PENDING, order?.Status);
        Assert.NotNull(order?.CustomerConfirmedDeliveryAt);
        Assert.Equal(ProjectStatus.DELIVERED, project?.Status);
        Assert.Equal(OrderItemStatus.DELIVERED, firstItem?.Status);
        Assert.Equal(OrderItemStatus.DELIVERED, secondItem?.Status);
        Assert.NotNull(firstItem?.DeliveredAt);
        Assert.NotNull(secondItem?.DeliveredAt);
        Assert.Equal(OrderItemStatus.PENDING, pendingItem?.Status);
        Assert.Equal(ProjectScheduleStatus.COMPLETED, linkedSchedule?.Status);
        Assert.Equal(
            1,
            await verification.PaymentSet.CountAsync(payment =>
                payment.OrderId == scenario.OrderId &&
                payment.PaymentType == PaymentType.REMAINING_PAYMENT &&
                payment.Status == PaymentStatus.PENDING));
    }

    [Fact]
    public async Task CreateDeliveryBatch_WhenScheduleNotConfirmed_ReturnsBadRequest()
    {
        var scenario = await SeedScenarioAsync();
        var schedule = await CreateDeliveryScheduleAsync(scenario, "Unconfirmed delivery");

        using var request = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Post,
            $"/orders/{scenario.OrderId}/deliveries",
            scenario.ProductionAccountId,
            CoreRoles.Production,
            new CreateDeliveryBatchRequestDto
            {
                ProjectScheduleId = schedule.ScheduleId,
                Items =
                [
                    new CreateDeliveryBatchItemRequestDto
                    {
                        OrderItemId = scenario.FirstOrderItemId,
                        Quantity = 1
                    }
                ]
            });

        var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var verification = _fixture.Database.CreateDbContext();
        var order = await verification.OrderSet.FindAsync(scenario.OrderId);
        Assert.Equal(OrderStatus.READY_FOR_DELIVERY, order?.Status);
    }

    [Fact]
    public async Task ConfirmDelivery_WhenStillDelivering_ReturnsBadRequest()
    {
        var scenario = await SeedScenarioAsync();
        var schedule = await CreateDeliveryScheduleAsync(scenario, "Incomplete delivery");

        await ConfirmScheduleAsync(scenario, schedule.ScheduleId);
        await CreateDeliveryBatchAsync(scenario, schedule.ScheduleId);

        using var request = IntegrationHttp.Authenticated(
            HttpMethod.Patch,
            $"/orders/{scenario.OrderId}/confirm-delivery",
            scenario.CustomerAccountId,
            CoreRoles.Customer);

        var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ConfirmDelivery_WhenAwaitingCustomerConfirmation_Succeeds()
    {
        var scenario = await SeedScenarioAsync();
        var schedule = await CreateDeliveryScheduleAsync(scenario, "Awaiting confirm delivery");

        await ConfirmScheduleAsync(scenario, schedule.ScheduleId);
        var batch = await CreateDeliveryBatchAsync(scenario, schedule.ScheduleId);
        await CompleteDeliveryBatchAsync(scenario, batch.DeliveryId);

        using var request = IntegrationHttp.Authenticated(
            HttpMethod.Patch,
            $"/orders/{scenario.OrderId}/confirm-delivery",
            scenario.CustomerAccountId,
            CoreRoles.Customer);

        var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var verification = _fixture.Database.CreateDbContext();
        var order = await verification.OrderSet.FindAsync(scenario.OrderId);
        Assert.Equal(OrderStatus.FINAL_PAYMENT_PENDING, order?.Status);
    }

    [Fact]
    public async Task ConfirmDelivery_WhenItemsNotPhysicallyDelivered_ReturnsConflict()
    {
        var scenario = await SeedScenarioAsync();
        var schedule = await CreateDeliveryScheduleAsync(scenario, "Incomplete delivery");

        await ConfirmScheduleAsync(scenario, schedule.ScheduleId);
        await CreateDeliveryBatchAsync(scenario, schedule.ScheduleId);

        await using (var context = _fixture.Database.CreateDbContext())
        {
            var order = await context.OrderSet.FindAsync(scenario.OrderId);
            Assert.NotNull(order);
            order!.Status = OrderStatus.AWAITING_CUSTOMER_CONFIRMATION;
            await context.SaveChangesAsync();
        }

        using var request = IntegrationHttp.Authenticated(
            HttpMethod.Patch,
            $"/orders/{scenario.OrderId}/confirm-delivery",
            scenario.CustomerAccountId,
            CoreRoles.Customer);

        var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CompleteDeliverySchedule_WhenBatchNotCompleted_ReturnsBadRequest()
    {
        var scenario = await SeedScenarioAsync();
        var schedule = await CreateDeliveryScheduleAsync(scenario, "Schedule only delivery");

        await ConfirmScheduleAsync(scenario, schedule.ScheduleId);
        await CreateDeliveryBatchAsync(scenario, schedule.ScheduleId);

        using var request = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Patch,
            $"/project-schedules/{schedule.ScheduleId}/status",
            scenario.ProductionAccountId,
            CoreRoles.Production,
            new UpdateProjectScheduleStatusRequestDto
            {
                Status = ProjectScheduleStatus.COMPLETED,
                Note = "Attempt schedule-only completion"
            });

        var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await using var verification = _fixture.Database.CreateDbContext();
        var order = await verification.OrderSet.FindAsync(scenario.OrderId);
        var project = await verification.ProjectSet.FindAsync(scenario.ProjectId);
        var linkedSchedule = await verification.ProjectScheduleSet.FindAsync(schedule.ScheduleId);

        Assert.Equal(OrderStatus.DELIVERING, order?.Status);
        Assert.Equal(ProjectStatus.DELIVERING, project?.Status);
        Assert.Equal(ProjectScheduleStatus.CONFIRMED, linkedSchedule?.Status);
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
            scenario.ProductionAccountId,
            CoreRoles.Production,
            new CreateProjectScheduleRequestDto
            {
                ScheduleType = ProjectScheduleType.DELIVERY,
                Title = title,
                Description = "Delivery appointment",
                AssignedStaffId = scenario.ProductionAccountId,
                ScheduledStart = ScheduleTestClock.VietnamLocalAsUtc(dayOffset: 1, hour: 8),
                ScheduledEnd = ScheduleTestClock.VietnamLocalAsUtc(dayOffset: 1, hour: 10),
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

    private async Task<DeliveryDetailDto> CreateDeliveryBatchAsync(
        DeliveryOrderScenario scenario,
        Guid scheduleId)
    {
        using var request = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Post,
            $"/orders/{scenario.OrderId}/deliveries",
            scenario.ProductionAccountId,
            CoreRoles.Production,
            new CreateDeliveryBatchRequestDto
            {
                ProjectScheduleId = scheduleId,
                Items =
                [
                    new CreateDeliveryBatchItemRequestDto
                    {
                        OrderItemId = scenario.FirstOrderItemId,
                        Quantity = 2
                    },
                    new CreateDeliveryBatchItemRequestDto
                    {
                        OrderItemId = scenario.SecondOrderItemId,
                        Quantity = 1
                    }
                ]
            });

        var response = await _fixture.Client.SendAsync(request);
        var batch = await ReadDataAsync<DeliveryDetailDto>(response, HttpStatusCode.Created);
        Assert.Equal(DeliveryStatus.IN_PROGRESS, batch.Status);
        Assert.Equal(scheduleId, batch.ProjectScheduleId);
        return batch;
    }

    private async Task<DeliveryBatchCompletionDto> CompleteDeliveryBatchAsync(
        DeliveryOrderScenario scenario,
        Guid deliveryId)
    {
        using var request = IntegrationHttp.Authenticated(
            HttpMethod.Patch,
            $"/orders/{scenario.OrderId}/deliveries/{deliveryId}/complete",
            scenario.ProductionAccountId,
            CoreRoles.Production);

        var response = await _fixture.Client.SendAsync(request);
        return await ReadDataAsync<DeliveryBatchCompletionDto>(response, HttpStatusCode.OK);
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
