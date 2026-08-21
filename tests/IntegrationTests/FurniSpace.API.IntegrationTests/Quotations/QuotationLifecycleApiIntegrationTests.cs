using System.Net;
using System.Net.Http.Json;
using FurniSpace.API.IntegrationTests.Fixtures;
using FurniSpace.API.IntegrationTests.Support;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Orders;
using FurniSpace.Application.DTOs.Production;
using FurniSpace.Application.DTOs.Quotations;
using FurniSpace.Application.Services.Production;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Testing.Seeding;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.API.IntegrationTests.Quotations;

[Collection(ApiIntegrationCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "Core")]
public sealed class QuotationLifecycleApiIntegrationTests : IAsyncLifetime
{
    private const string RevisionReason = "Please revise material and price.";
    private const decimal ProductUnitPrice = 2_500_000m;
    private const decimal ProductDiscount = 500_000m;
    private const decimal ProductGross = 2_500_000m;
    private const decimal ProductPreVatTotal = 2_000_000m;
    private const decimal ProductVatShare = 160_000m;
    private const decimal ProductVatInclusiveTotal = 2_160_000m;
    private const decimal ProductDepositAmount = 648_000m;
    private const decimal DraftPreVatTotal = 10_000_000m;
    private const decimal DraftVatAmount = 800_000m;
    private const decimal DraftTotalAmount = 10_800_000m;

    private readonly ApiIntegrationFixture _fixture;

    public QuotationLifecycleApiIntegrationTests(ApiIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.Database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateDraft_WhenProjectHasSelectedProposal_PersistsProductSnapshots()
    {
        var scenario = await SeedSelectedProposalAsync();

        var response = await CreateDraftAsync(scenario);
        var result = await ReadQuotationAsync(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal(QuotationStatus.DRAFT, result.Data.Status);
        Assert.Equal(DraftTotalAmount, result.Data.TotalAmount);
        Assert.Equal(DraftPreVatTotal, result.Data.PreVatAmount);
        Assert.Equal(DraftVatAmount, result.Data.VatAmount);
        Assert.Single(result.Data.Items);

        var item = result.Data.Items[0];
        Assert.Equal(scenario.ProposalItemId, item.ProposalItemId);
        Assert.Equal(scenario.ProductVersionId, item.ProductVersionId);
        Assert.Equal(DraftPreVatTotal, item.TotalAmount);

        await using var context = _fixture.Database.CreateDbContext();
        var persistedQuotation = await context.QuotationSet.SingleAsync();
        var persistedItem = await context.QuotationItemSet.SingleAsync();
        Assert.Equal(scenario.ProposalId, persistedQuotation.ProposalId);
        Assert.Equal(scenario.ProductVersionId, persistedItem.ProductVersionId);
        Assert.Equal(scenario.ProposalItemId, persistedItem.ProposalItemId);
    }

    [Fact]
    public async Task CreateDraft_WhenCustomerCalls_ReturnsForbiddenAndCreatesNothing()
    {
        var scenario = await SeedSelectedProposalAsync();
        using var request = IntegrationHttp.Authenticated(
            HttpMethod.Post,
            $"/projects/{scenario.ProjectId}/quotations",
            scenario.CustomerAccountId,
            CoreRoles.Customer);

        var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var context = _fixture.Database.CreateDbContext();
        Assert.Equal(0, await context.QuotationSet.CountAsync());
    }

    [Fact]
    public async Task Send_WhenDraftIsReady_MovesQuotationAndProjectToSent()
    {
        var scenario = await SeedSelectedProposalAsync();
        var quotationId = await CreateReadyDraftAsync(scenario);

        var response = await SendAsync(scenario.SalesAccountId, quotationId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertQuotationStateAsync(quotationId, QuotationStatus.SENT, ProjectStatus.QUOTATION_SENT);
    }

    [Fact]
    public async Task Send_WhenMissingValidUntil_ReturnsBadRequestAndKeepsDraft()
    {
        var scenario = await SeedSelectedProposalAsync();
        var draftResponse = await CreateDraftAsync(scenario);
        var draft = await ReadQuotationAsync(draftResponse);
        Assert.NotNull(draft.Data);

        var response = await SendAsync(scenario.SalesAccountId, draft.Data.QuotationId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertQuotationStateAsync(
            draft.Data.QuotationId,
            QuotationStatus.DRAFT,
            ProjectStatus.PROPOSAL_SELECTED);
    }

    [Fact]
    public async Task RevisionFlow_WhenCustomerRequestsRevision_SalesCanReviseAndSendAgain()
    {
        var scenario = await SeedSelectedProposalAsync();
        var quotationId = await CreateReadyDraftAsync(scenario);
        var sentResponse = await SendAsync(scenario.SalesAccountId, quotationId);
        Assert.Equal(HttpStatusCode.OK, sentResponse.StatusCode);

        var revisionResponse = await RequestRevisionAsync(scenario, quotationId);
        Assert.Equal(HttpStatusCode.OK, revisionResponse.StatusCode);
        await AssertQuotationStateAsync(
            quotationId,
            QuotationStatus.REVISION_REQUESTED,
            ProjectStatus.QUOTATION_REVISION_REQUESTED);

        var reviseResponse = await ReviseAsync(scenario.SalesAccountId, quotationId);
        var revised = await ReadQuotationAsync(reviseResponse);
        Assert.Equal(HttpStatusCode.OK, reviseResponse.StatusCode);
        Assert.NotNull(revised.Data);
        Assert.Equal(2, revised.Data.VersionNo);
        Assert.Equal(QuotationStatus.REVISED, revised.Data.Status);

        var resendResponse = await SendAsync(scenario.SalesAccountId, quotationId);
        Assert.Equal(HttpStatusCode.OK, resendResponse.StatusCode);
        await AssertQuotationStateAsync(quotationId, QuotationStatus.SENT, ProjectStatus.QUOTATION_SENT);
    }

    [Fact]
    public async Task RequestRevision_WhenSalesCalls_ReturnsForbidden()
    {
        var scenario = await SeedSelectedProposalAsync();
        var quotationId = await CreateReadyDraftAsync(scenario);
        var sentResponse = await SendAsync(scenario.SalesAccountId, quotationId);
        Assert.Equal(HttpStatusCode.OK, sentResponse.StatusCode);

        using var request = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Patch,
            $"/quotations/{quotationId}/request-revision",
            scenario.SalesAccountId,
            CoreRoles.Sales,
            new RequestQuotationRevisionDto { RevisionReason = RevisionReason });

        var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertQuotationStateAsync(quotationId, QuotationStatus.SENT, ProjectStatus.QUOTATION_SENT);
    }

    [Fact]
    public async Task FinancialLifecycle_WhenProductBecomesUnavailable_CompletesProductionWithoutAdjustments()
    {
        var scenario = await SeedSelectedProposalAsync();
        var productionAccountId = await SeedProductionAccountAsync();
        var quotationId = await CreateDraftIdAsync(scenario);
        var productQuotationItemId = await GetProductQuotationItemIdAsync(quotationId);

        await UpdateProductFinancialsAsync(scenario, quotationId, productQuotationItemId);
        await UpdateValidUntilAsync(scenario.SalesAccountId, quotationId, ProductDepositAmount);
        await SendAsync(scenario.SalesAccountId, quotationId);
        await AcceptAsync(scenario, quotationId);

        var orderSnapshot = await AssertAcceptedOrderSnapshotsAsync(
            scenario,
            quotationId,
            productQuotationItemId);

        await MarkDepositPaidAsync(scenario, quotationId, orderSnapshot.OrderId);
        var productionRequestId = await CreateProductionRequestAsync(
            scenario,
            orderSnapshot.OrderId,
            productionAccountId);
        var productionItemId = await GetProductionItemIdAsync(productionRequestId);
        await StartProductionAsync(productionAccountId, productionRequestId);
        await UpdateProductionItemStatusAsync(
            productionAccountId,
            productionItemId,
            ProductionItemStatus.CANCELLED,
            "Material unavailable");

        using var completeRequest = IntegrationHttp.Authenticated(
            HttpMethod.Patch,
            $"/production-requests/{productionRequestId}/complete",
            productionAccountId,
            CoreRoles.Production);
        var completeResponse = await _fixture.Client.SendAsync(completeRequest);
        var completion = await ReadDataAsync<ProductionCompletionDto>(completeResponse, HttpStatusCode.OK);

        Assert.Equal("COMPLETED", completion.ProductionStatus);
        Assert.Equal("READY_FOR_DELIVERY", completion.OrderStatus);

        await AssertUnavailableProductionCompletionStateAsync(
            orderSnapshot.OrderId,
            orderSnapshot.ProductOrderItemId,
            productionRequestId);
    }

    private async Task<QuotationDraftScenario> SeedSelectedProposalAsync()
    {
        await using var context = _fixture.Database.CreateDbContext();
        return await QuotationAcceptScenarioSeeder.SeedSelectedProposalForQuotationAsync(context);
    }

    private async Task<Guid> SeedProductionAccountAsync()
    {
        await using var context = _fixture.Database.CreateDbContext();
        var production = await CoreAccountSeeder.SeedAccountAsync(context, CoreRoles.Production);
        return production.AccountId;
    }

    private async Task<HttpResponseMessage> CreateDraftAsync(QuotationDraftScenario scenario)
    {
        using var request = IntegrationHttp.Authenticated(
            HttpMethod.Post,
            $"/projects/{scenario.ProjectId}/quotations",
            scenario.SalesAccountId,
            CoreRoles.Sales);

        return await _fixture.Client.SendAsync(request);
    }

    private async Task<Guid> CreateReadyDraftAsync(QuotationDraftScenario scenario)
    {
        var quotationId = await CreateDraftIdAsync(scenario);

        var updateResponse = await UpdateValidUntilAsync(scenario.SalesAccountId, quotationId);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        return quotationId;
    }

    private async Task<Guid> CreateDraftIdAsync(QuotationDraftScenario scenario)
    {
        var createResponse = await CreateDraftAsync(scenario);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var draft = await ReadQuotationAsync(createResponse);
        Assert.NotNull(draft.Data);
        return draft.Data.QuotationId;
    }

    private Task<HttpResponseMessage> UpdateValidUntilAsync(Guid salesId, Guid quotationId)
        => UpdateValidUntilAsync(salesId, quotationId, depositAmount: null);

    private async Task<HttpResponseMessage> UpdateValidUntilAsync(
        Guid salesId,
        Guid quotationId,
        decimal? depositAmount)
    {
        using var request = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Patch,
            $"/quotations/{quotationId}",
            salesId,
            CoreRoles.Sales,
            new UpdateQuotationRequestDto
            {
                ValidUntil = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
                DepositAmount = depositAmount
            });

        return await _fixture.Client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendAsync(Guid salesId, Guid quotationId)
    {
        using var request = IntegrationHttp.Authenticated(
            HttpMethod.Patch,
            $"/quotations/{quotationId}/send",
            salesId,
            CoreRoles.Sales);

        return await _fixture.Client.SendAsync(request);
    }

    private async Task AcceptAsync(QuotationDraftScenario scenario, Guid quotationId)
    {
        using var request = IntegrationHttp.Authenticated(
            HttpMethod.Patch,
            $"/quotations/{quotationId}/accept",
            scenario.CustomerAccountId,
            CoreRoles.Customer);

        var response = await _fixture.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task UpdateProductFinancialsAsync(
        QuotationDraftScenario scenario,
        Guid quotationId,
        Guid quotationItemId)
    {
        using var request = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Patch,
            $"/quotations/{quotationId}/items/{quotationItemId}/financials",
            scenario.SalesAccountId,
            CoreRoles.Sales,
            new UpdateQuotationItemFinancialsRequestDto
            {
                Quantity = 1,
                UnitPrice = ProductUnitPrice,
                DiscountAmount = ProductDiscount
            });

        var response = await _fixture.Client.SendAsync(request);
        var quotation = await ReadQuotationAsync(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(quotation.Data);

        var item = quotation.Data.Items.Single(entry => entry.QuotationItemId == quotationItemId);
        Assert.Equal(ProductGross, item.GrossAmount);
        Assert.Equal(ProductDiscount, item.DiscountAmount);
        Assert.Equal(ProductPreVatTotal, item.TotalAmount);
        Assert.Equal(ProductVatInclusiveTotal, quotation.Data.TotalAmount);
    }

    private async Task<Guid> CreateProductionRequestAsync(
        QuotationDraftScenario scenario,
        Guid orderId,
        Guid productionAccountId)
    {
        using var request = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Post,
            $"/orders/{orderId}/production-request",
            scenario.SalesAccountId,
            CoreRoles.Sales,
            new CreateProductionRequestDto
            {
                AssignedTo = productionAccountId,
                Priority = "NORMAL",
                EstimatedStartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
                EstimatedCompletionDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
                Note = "Create production from quotation E2E"
            });

        var response = await _fixture.Client.SendAsync(request);
        var result = await ReadDataAsync<ProductionRequestCreatedDto>(response, HttpStatusCode.Created);
        Assert.Equal(1, result.ProductionItemCount);
        return result.ProductionRequestId;
    }

    private async Task StartProductionAsync(Guid productionAccountId, Guid productionRequestId)
    {
        using var request = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Patch,
            $"/production-requests/{productionRequestId}/start",
            productionAccountId,
            CoreRoles.Production,
            new StartProductionRequestDto());

        var response = await _fixture.Client.SendAsync(request);
        await ReadDataAsync<ProductionRequestStatusDto>(response, HttpStatusCode.OK);
    }

    private async Task UpdateProductionItemStatusAsync(
        Guid productionAccountId,
        Guid productionItemId,
        ProductionItemStatus status,
        string? cancellationReason = null)
    {
        using var request = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Patch,
            $"/production-items/{productionItemId}/status",
            productionAccountId,
            CoreRoles.Production,
            new UpdateProductionItemStatusDto
            {
                Status = status,
                ProductionNote = $"{status} update",
                CancellationReason = cancellationReason
            });

        var response = await _fixture.Client.SendAsync(request);
        await ReadDataAsync<ProductionItemStatusDto>(response, HttpStatusCode.OK);
    }

    private async Task AssertUnavailableProductionCompletionStateAsync(
        Guid orderId,
        Guid productOrderItemId,
        Guid productionRequestId)
    {
        await using var context = _fixture.Database.CreateDbContext();
        var order = await context.OrderSet.SingleAsync(item => item.OrderId == orderId);
        var productItem = await context.OrderItemSet.SingleAsync(item => item.OrderItemId == productOrderItemId);
        var productionRequest = await context.ProductionRequestSet.SingleAsync(
            item => item.ProductionRequestId == productionRequestId);

        Assert.Equal(OrderStatus.READY_FOR_DELIVERY, order.Status);
        Assert.Equal(ProductVatInclusiveTotal, order.FinalTotalAmount);
        Assert.Equal(OrderItemStatus.UNAVAILABLE, productItem.Status);
        Assert.Equal("Material unavailable", productItem.UnavailableReason);
        Assert.Equal(ProductionRequestStatus.COMPLETED, productionRequest.Status);
    }

    private async Task<HttpResponseMessage> RequestRevisionAsync(
        QuotationDraftScenario scenario,
        Guid quotationId)
    {
        using var request = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Patch,
            $"/quotations/{quotationId}/request-revision",
            scenario.CustomerAccountId,
            CoreRoles.Customer,
            new RequestQuotationRevisionDto { RevisionReason = RevisionReason });

        return await _fixture.Client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> ReviseAsync(Guid salesId, Guid quotationId)
    {
        using var request = IntegrationHttp.Authenticated(
            HttpMethod.Patch,
            $"/quotations/{quotationId}/revise",
            salesId,
            CoreRoles.Sales);

        return await _fixture.Client.SendAsync(request);
    }

    private static async Task<ServiceResult<QuotationDetailDto>> ReadQuotationAsync(
        HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<ServiceResult<QuotationDetailDto>>(
            IntegrationHttp.JsonOptions) ?? new ServiceResult<QuotationDetailDto>();

    private static async Task<T> ReadDataAsync<T>(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus)
        where T : class
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == expectedStatus,
            $"Expected {expectedStatus}, got {response.StatusCode}. Body: {body}");
        var result = await response.Content.ReadFromJsonAsync<ServiceResult<T>>(IntegrationHttp.JsonOptions);
        Assert.NotNull(result?.Data);
        return result.Data;
    }

    private async Task<Guid> GetProductQuotationItemIdAsync(Guid quotationId)
    {
        await using var context = _fixture.Database.CreateDbContext();
        return await context.QuotationItemSet
            .Where(item => item.QuotationId == quotationId && item.ProposalItemId != null)
            .Select(item => item.QuotationItemId)
            .SingleAsync();
    }

    private async Task<Guid> GetProductionItemIdAsync(Guid productionRequestId)
    {
        await using var context = _fixture.Database.CreateDbContext();
        return await context.ProductionItemSet
            .Where(item => item.ProductionRequestId == productionRequestId)
            .Select(item => item.ProductionItemId)
            .SingleAsync();
    }

    private async Task MarkDepositPaidAsync(
        QuotationDraftScenario scenario,
        Guid quotationId,
        Guid orderId)
    {
        await using var context = _fixture.Database.CreateDbContext();
        var order = await context.OrderSet.SingleAsync(item => item.OrderId == orderId);
        order.Status = OrderStatus.DEPOSIT_PAID;
        order.PaidAmount = order.DepositAmount;
        order.RemainingAmount = order.FinalTotalAmount - order.PaidAmount;
        order.UpdatedAt = DateTime.UtcNow;

        context.PaymentSet.Add(new Payment
        {
            PaymentId = Guid.NewGuid(),
            ProjectId = scenario.ProjectId,
            OrderId = orderId,
            QuotationId = quotationId,
            PaymentCode = $"PAY-E2E-{Guid.NewGuid():N}"[..24],
            PaidBy = scenario.CustomerAccountId,
            PaymentType = PaymentType.DEPOSIT,
            Amount = order.DepositAmount ?? 0m,
            Currency = "VND",
            Status = PaymentStatus.PAID,
            PaidAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
    }

    private async Task<AcceptedOrderSnapshot> AssertAcceptedOrderSnapshotsAsync(
        QuotationDraftScenario scenario,
        Guid quotationId,
        Guid productQuotationItemId)
    {
        await using var context = _fixture.Database.CreateDbContext();
        var quotation = await context.QuotationSet.SingleAsync(item => item.QuotationId == quotationId);
        var order = await context.OrderSet.SingleAsync(item => item.QuotationId == quotationId);
        var orderItems = await context.OrderItemSet
            .Where(item => item.OrderId == order.OrderId)
            .ToListAsync();

        Assert.Equal(QuotationStatus.ACCEPTED, quotation.Status);
        Assert.Equal(OrderStatus.CREATED, order.Status);
        Assert.Equal(ProductVatInclusiveTotal, quotation.TotalAmount);
        Assert.Equal(ProductDepositAmount, quotation.DepositAmount);
        Assert.Equal(ProductVatInclusiveTotal, order.OriginalTotalAmount);
        Assert.Equal(ProductVatInclusiveTotal, order.FinalTotalAmount);
        Assert.Equal(ProductVatInclusiveTotal, order.RemainingAmount);
        Assert.Equal(ProductDepositAmount, order.DepositAmount);
        Assert.Equal(0.08m, order.VatRate);
        Assert.Equal(ProductVatShare, order.VatAmount);
        Assert.Equal(scenario.CustomerAccountId, order.CustomerId);
        Assert.Equal(scenario.SalesAccountId, order.SalesId);
        Assert.Single(orderItems);

        var productOrderItem = orderItems.Single(item => item.QuotationItemId == productQuotationItemId);
        Assert.Equal(1, productOrderItem.Quantity);
        Assert.Equal(ProductUnitPrice, productOrderItem.UnitPrice);
        Assert.Equal(ProductDiscount, productOrderItem.DiscountAmount);
        Assert.Equal(ProductPreVatTotal, productOrderItem.SubtotalAmount);

        return new AcceptedOrderSnapshot(order.OrderId, productOrderItem.OrderItemId);
    }

    private async Task AssertQuotationStateAsync(
        Guid quotationId,
        QuotationStatus quotationStatus,
        ProjectStatus projectStatus)
    {
        await using var context = _fixture.Database.CreateDbContext();
        var quotation = await context.QuotationSet.SingleAsync(item => item.QuotationId == quotationId);
        var project = await context.ProjectSet.SingleAsync(item => item.ProjectId == quotation.ProjectId);
        Assert.Equal(quotationStatus, quotation.Status);
        Assert.Equal(projectStatus, project.Status);
    }

    private sealed record AcceptedOrderSnapshot(Guid OrderId, Guid ProductOrderItemId);
}
