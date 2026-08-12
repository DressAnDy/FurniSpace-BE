using System.Net;
using System.Net.Http.Json;
using FurniSpace.API.IntegrationTests.Fixtures;
using FurniSpace.API.IntegrationTests.Support;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Production;
using FurniSpace.Domain.Enums;
using FurniSpace.Testing.Seeding;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.API.IntegrationTests.Production;

[Collection(ApiIntegrationCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "Core")]
public sealed class ProductionWorkflowApiIntegrationTests : IAsyncLifetime
{
    private const string NormalPriority = "NORMAL";

    private readonly ApiIntegrationFixture _fixture;

    public ProductionWorkflowApiIntegrationTests(ApiIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.Database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateProductionRequest_WhenDepositPaid_CreatesProductItemsAndMovesWorkflow()
    {
        var scenario = await SeedScenarioAsync();

        var response = await CreateProductionRequestAsync(scenario);
        var created = await ReadDataAsync<ProductionRequestCreatedDto>(response, HttpStatusCode.Created);

        Assert.Equal(scenario.OrderId, created.OrderId);
        Assert.Equal(scenario.ProjectId, created.ProjectId);
        Assert.Equal(scenario.ProductionAccountId, created.AssignedTo);
        Assert.Equal(nameof(ProductionRequestStatus.PENDING_REVIEW), created.Status);
        Assert.Equal(1, created.ProductionItemCount);

        await using var verification = _fixture.Database.CreateDbContext();
        var request = await verification.ProductionRequestSet.SingleAsync();
        var productOrderItem = await verification.OrderItemSet.FindAsync(scenario.ProductOrderItemId);
        var order = await verification.OrderSet.FindAsync(scenario.OrderId);
        var project = await verification.ProjectSet.FindAsync(scenario.ProjectId);

        Assert.Equal(NormalPriority, request.Priority);
        Assert.Equal("Build product items", request.Note);
        Assert.Equal(OrderItemStatus.IN_PRODUCTION, productOrderItem?.Status);
        Assert.Equal(OrderStatus.IN_PRODUCTION, order?.Status);
        Assert.Equal(ProjectStatus.IN_PRODUCTION, project?.Status);
    }

    [Fact]
    public async Task CreateProductionRequest_WhenDepositPaymentIsNotPaid_ReturnsBadRequest()
    {
        var scenario = await SeedScenarioAsync();
        await using (var context = _fixture.Database.CreateDbContext())
        {
            var payment = await context.PaymentSet.SingleAsync(payment => payment.OrderId == scenario.OrderId);
            payment.Status = PaymentStatus.PENDING;
            await context.SaveChangesAsync();
        }

        var response = await CreateProductionRequestAsync(scenario);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var verification = _fixture.Database.CreateDbContext();
        Assert.Empty(await verification.ProductionRequestSet.ToListAsync());
    }

    [Fact]
    public async Task GetAvailableStaff_ReturnsOnlyActiveProductionStaff()
    {
        var scenario = await SeedScenarioAsync();

        using var request = IntegrationHttp.Authenticated(
            HttpMethod.Get,
            "/production-staff/available?search=Production",
            scenario.SalesAccountId,
            CoreRoles.Sales);

        var response = await _fixture.Client.SendAsync(request);
        var staff = await ReadDataAsync<List<AvailableProductionStaffDto>>(response, HttpStatusCode.OK);
        var staffIds = staff.Select(item => item.AccountId).ToHashSet();

        Assert.Contains(scenario.ProductionAccountId, staffIds);
        Assert.Contains(scenario.SecondProductionAccountId, staffIds);
        Assert.DoesNotContain(scenario.InactiveProductionAccountId, staffIds);
        Assert.All(staff, item => Assert.Equal(nameof(AccountStatus.ACTIVE), item.AccountStatus));
    }

    [Fact]
    public async Task AssignProductionRequest_ReassignsToAnotherActiveProductionStaff()
    {
        var scenario = await SeedScenarioAsync();
        var created = await CreateRequestDataAsync(scenario);

        using var assignRequest = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Patch,
            $"/production-requests/{created.ProductionRequestId}/assign",
            scenario.SalesAccountId,
            CoreRoles.Sales,
            new AssignProductionRequestDto
            {
                AssignedTo = scenario.SecondProductionAccountId,
                AssignmentNote = "Move to second staff"
            });

        var response = await _fixture.Client.SendAsync(assignRequest);
        var assigned = await ReadDataAsync<ProductionRequestAssignmentDto>(response, HttpStatusCode.OK);

        Assert.Equal(scenario.ProductionAccountId, assigned.PreviousAssignedTo);
        Assert.Equal(scenario.SecondProductionAccountId, assigned.AssignedTo);

        await using var verification = _fixture.Database.CreateDbContext();
        var request = await verification.ProductionRequestSet.FindAsync(created.ProductionRequestId);
        Assert.Equal(scenario.SecondProductionAccountId, request?.AssignedTo);
        Assert.Contains("Move to second staff", request?.Note, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompleteProduction_WhenItemsCompleted_MovesOrderAndProjectReadyForDelivery()
    {
        var scenario = await SeedScenarioAsync();
        var created = await CreateRequestDataAsync(scenario);
        var productionItemId = await GetProductionItemIdAsync(created.ProductionRequestId);

        await MarkFeasibleAsync(scenario, created.ProductionRequestId);
        await StartProductionAsync(scenario, created.ProductionRequestId);
        await UpdateItemStatusAsync(scenario, productionItemId, ProductionItemStatus.IN_PRODUCTION);
        await UpdateItemStatusAsync(scenario, productionItemId, ProductionItemStatus.COMPLETED);

        var completions = await Task.WhenAll(
            CompleteProductionAsync(scenario, created.ProductionRequestId),
            CompleteProductionAsync(scenario, created.ProductionRequestId));
        var firstCompletion = completions[0];
        var secondCompletion = completions[1];

        Assert.Equal(nameof(ProductionRequestStatus.COMPLETED), firstCompletion.ProductionStatus);
        Assert.Equal(nameof(OrderStatus.READY_FOR_DELIVERY), firstCompletion.OrderStatus);
        Assert.Equal(nameof(ProjectStatus.READY_FOR_DELIVERY), firstCompletion.ProjectStatus);
        Assert.Equal(firstCompletion.ProductionStatus, secondCompletion.ProductionStatus);

        await using var verification = _fixture.Database.CreateDbContext();
        var productOrderItem = await verification.OrderItemSet.FindAsync(scenario.ProductOrderItemId);
        var order = await verification.OrderSet.FindAsync(scenario.OrderId);
        var project = await verification.ProjectSet.FindAsync(scenario.ProjectId);

        Assert.Equal(OrderItemStatus.READY, productOrderItem?.Status);
        Assert.Equal(OrderStatus.READY_FOR_DELIVERY, order?.Status);
        Assert.Equal(ProjectStatus.READY_FOR_DELIVERY, project?.Status);
    }

    [Fact]
    public async Task CompleteProduction_WhenItemsAreUnresolved_ReturnsBadRequest()
    {
        var scenario = await SeedScenarioAsync();
        var created = await CreateRequestDataAsync(scenario);

        await MarkFeasibleAsync(scenario, created.ProductionRequestId);
        await StartProductionAsync(scenario, created.ProductionRequestId);

        using var request = IntegrationHttp.Authenticated(
            HttpMethod.Patch,
            $"/production-requests/{created.ProductionRequestId}/complete",
            scenario.ProductionAccountId,
            CoreRoles.Production);

        var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CompleteProduction_WhenCancelledItemHasNoConfirmedAdjustment_CompletesSuccessfully()
    {
        var scenario = await SeedScenarioAsync();
        var created = await CreateRequestDataAsync(scenario);
        var productionItemId = await GetProductionItemIdAsync(created.ProductionRequestId);

        await MarkFeasibleAsync(scenario, created.ProductionRequestId);
        await StartProductionAsync(scenario, created.ProductionRequestId);
        await UpdateItemStatusAsync(scenario, productionItemId, ProductionItemStatus.CANCELLED, "Material unavailable");

        using var request = IntegrationHttp.Authenticated(
            HttpMethod.Patch,
            $"/production-requests/{created.ProductionRequestId}/complete",
            scenario.ProductionAccountId,
            CoreRoles.Production);

        var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var verification = _fixture.Database.CreateDbContext();
        var orderItem = await verification.OrderItemSet.FindAsync(scenario.ProductOrderItemId);
        var order = await verification.OrderSet.FindAsync(scenario.OrderId);
        Assert.Equal(OrderItemStatus.UNAVAILABLE, orderItem?.Status);
        Assert.Equal("Material unavailable", orderItem?.UnavailableReason);
        Assert.Equal(OrderStatus.READY_FOR_DELIVERY, order?.Status);
    }

    private async Task<ProductionOrderScenario> SeedScenarioAsync()
    {
        await using var context = _fixture.Database.CreateDbContext();
        return await ProductionScenarioSeeder.SeedDepositPaidOrderAsync(context);
    }

    private async Task<ProductionRequestCreatedDto> CreateRequestDataAsync(ProductionOrderScenario scenario)
    {
        var response = await CreateProductionRequestAsync(scenario);
        return await ReadDataAsync<ProductionRequestCreatedDto>(response, HttpStatusCode.Created);
    }

    private async Task<HttpResponseMessage> CreateProductionRequestAsync(ProductionOrderScenario scenario)
    {
        using var request = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Post,
            $"/orders/{scenario.OrderId}/production-request",
            scenario.SalesAccountId,
            CoreRoles.Sales,
            new CreateProductionRequestDto
            {
                AssignedTo = scenario.ProductionAccountId,
                Priority = $" {NormalPriority.ToLowerInvariant()} ",
                EstimatedStartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
                EstimatedCompletionDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
                Note = " Build product items "
            });

        return await _fixture.Client.SendAsync(request);
    }

    private async Task MarkFeasibleAsync(ProductionOrderScenario scenario, Guid productionRequestId)
    {
        using var request = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Patch,
            $"/production-requests/{productionRequestId}/mark-feasible",
            scenario.ProductionAccountId,
            CoreRoles.Production,
            new MarkProductionRequestFeasibleDto { Note = "Feasible" });

        var response = await _fixture.Client.SendAsync(request);
        var status = await ReadDataAsync<ProductionRequestStatusDto>(response, HttpStatusCode.OK);
        Assert.Equal(nameof(ProductionRequestStatus.FEASIBLE), status.Status);
    }

    private async Task StartProductionAsync(ProductionOrderScenario scenario, Guid productionRequestId)
    {
        using var request = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Patch,
            $"/production-requests/{productionRequestId}/start",
            scenario.ProductionAccountId,
            CoreRoles.Production,
            new StartProductionRequestDto
            {
                ActualStartDate = DateOnly.FromDateTime(DateTime.UtcNow)
            });

        var response = await _fixture.Client.SendAsync(request);
        var status = await ReadDataAsync<ProductionRequestStatusDto>(response, HttpStatusCode.OK);
        Assert.Equal(nameof(ProductionRequestStatus.IN_PRODUCTION), status.Status);
    }

    private async Task UpdateItemStatusAsync(
        ProductionOrderScenario scenario,
        Guid productionItemId,
        ProductionItemStatus status,
        string? cancellationReason = null)
    {
        using var request = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Patch,
            $"/production-items/{productionItemId}/status",
            scenario.ProductionAccountId,
            CoreRoles.Production,
            new UpdateProductionItemStatusDto
            {
                Status = status,
                ProductionNote = $"{status} update",
                CancellationReason = cancellationReason
            });

        var response = await _fixture.Client.SendAsync(request);
        var item = await ReadDataAsync<ProductionItemStatusDto>(response, HttpStatusCode.OK);
        Assert.Equal(status.ToString(), item.Status);
    }

    private async Task<ProductionCompletionDto> CompleteProductionAsync(
        ProductionOrderScenario scenario,
        Guid productionRequestId)
    {
        using var request = IntegrationHttp.Authenticated(
            HttpMethod.Patch,
            $"/production-requests/{productionRequestId}/complete",
            scenario.ProductionAccountId,
            CoreRoles.Production);

        var response = await _fixture.Client.SendAsync(request);
        return await ReadDataAsync<ProductionCompletionDto>(response, HttpStatusCode.OK);
    }

    private async Task<Guid> GetProductionItemIdAsync(Guid productionRequestId)
    {
        await using var context = _fixture.Database.CreateDbContext();
        return await context.ProductionItemSet
            .Where(item => item.ProductionRequestId == productionRequestId)
            .Select(item => item.ProductionItemId)
            .SingleAsync();
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
