using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using FurniSpace.API.IntegrationTests.Fixtures;
using FurniSpace.API.IntegrationTests.Support;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Orders;
using FurniSpace.Application.DTOs.Payments;
using FurniSpace.Application.DTOs.Projects;
using FurniSpace.Application.Interfaces.Payments;
using FurniSpace.Domain.Enums;
using FurniSpace.Testing.Fakes;
using FurniSpace.Testing.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FurniSpace.API.IntegrationTests.Orders;

[Collection(ApiIntegrationCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "Core")]
public sealed class FinalPaymentReviewApiIntegrationTests : IAsyncLifetime
{
    private const string ReturnUrl = "https://frontend.integration.test/payments/remaining-return";
    private const string CancelUrl = "https://frontend.integration.test/payments/remaining-cancel";
    private const string WebhookPayload = "{}";
    private const string WebhookReference = "PAYOS-REMAINING-INTEGRATION";

    private readonly ApiIntegrationFixture _fixture;

    public FinalPaymentReviewApiIntegrationTests(ApiIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.Database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task PrepareFinalPayment_WhenRemainingExists_AutoCompletesOrderAfterPaymentWithoutManualComplete()
    {
        var scenario = await SeedDeliveredWithRemainingAsync();
        var preparation = await PrepareFinalPaymentAsync(scenario);
        var payment = await CreateRemainingPaymentAsync(scenario);

        Assert.Equal(nameof(OrderStatus.FINAL_PAYMENT_PENDING), preparation.Status);
        Assert.True(preparation.RequiresRemainingPayment);
        Assert.Equal(scenario.RemainingAmount, preparation.RemainingAmount);
        Assert.Equal(PaymentType.REMAINING_PAYMENT, payment.PaymentType);
        Assert.Equal(scenario.RemainingAmount, payment.Amount);

        await PayRemainingPaymentAsync(scenario, payment.PaymentId, payment.Amount);

        await using var verification = _fixture.Database.CreateDbContext();
        var order = await verification.OrderSet.FindAsync(scenario.OrderId);
        var project = await verification.ProjectSet.FindAsync(scenario.ProjectId);
        Assert.Equal(OrderStatus.COMPLETED, order?.Status);
        Assert.Equal(ProjectStatus.DELIVERED, project?.Status);
        Assert.Equal(scenario.FinalTotalAmount, order?.PaidAmount);
        Assert.Equal(0m, order?.RemainingAmount);
    }

    [Fact]
    public async Task CompleteProject_WhenOrderAutoCompletedAfterPayment_CompletesProjectIdempotently()
    {
        var scenario = await SeedDeliveredWithRemainingAsync();
        await PrepareFinalPaymentAsync(scenario);
        var payment = await CreateRemainingPaymentAsync(scenario);
        await PayRemainingPaymentAsync(scenario, payment.PaymentId, payment.Amount);

        var firstCompletion = await CompleteProjectAsync(scenario);
        var secondCompletion = await CompleteProjectAsync(scenario);

        Assert.Equal(nameof(ProjectStatus.COMPLETED), firstCompletion.ProjectStatus);
        Assert.Equal(firstCompletion.ProjectStatus, secondCompletion.ProjectStatus);

        await using var verification = _fixture.Database.CreateDbContext();
        Assert.Equal(OrderStatus.COMPLETED, (await verification.OrderSet.FindAsync(scenario.OrderId))?.Status);
        Assert.Equal(ProjectStatus.COMPLETED, (await verification.ProjectSet.FindAsync(scenario.ProjectId))?.Status);
    }

    [Fact]
    public async Task PrepareFinalPayment_WhenOrderAlreadyFullyPaid_DoesNotRequireRemainingPayment()
    {
        var scenario = await SeedDeliveredFullyPaidAsync();

        var preparation = await PrepareFinalPaymentAsync(scenario);

        Assert.Equal(nameof(OrderStatus.DELIVERED), preparation.Status);
        Assert.False(preparation.RequiresRemainingPayment);
        Assert.Equal(0m, preparation.RemainingAmount);

        await using var verification = _fixture.Database.CreateDbContext();
        Assert.Equal(1, await verification.PaymentSet.CountAsync());
        Assert.Equal(OrderStatus.DELIVERED, (await verification.OrderSet.FindAsync(scenario.OrderId))?.Status);
    }

    [Fact]
    public async Task CreateRemainingPayment_WhenOrderIsNotPrepared_ReturnsBadRequest()
    {
        var scenario = await SeedDeliveredWithRemainingAsync();

        using var request = BuildRemainingPaymentRequest(scenario);
        var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var verification = _fixture.Database.CreateDbContext();
        Assert.Single(await verification.PaymentSet.ToListAsync());
    }

    [Fact]
    public async Task CompleteOrder_WhenRemainingPaymentNotPaid_ReturnsBadRequest()
    {
        var scenario = await SeedDeliveredWithRemainingAsync();
        await PrepareFinalPaymentAsync(scenario);

        using var request = IntegrationHttp.Authenticated(
            HttpMethod.Patch,
            $"/orders/{scenario.OrderId}/complete",
            scenario.SalesAccountId,
            CoreRoles.Sales);

        var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PrepareFinalPayment_WhenDeliveryNotConfirmed_ReturnsBadRequest()
    {
        var scenario = await SeedDeliveredWithRemainingAsync();
        await using (var context = _fixture.Database.CreateDbContext())
        {
            var order = await context.OrderSet.FindAsync(scenario.OrderId);
            order!.CustomerConfirmedDeliveryAt = null;
            await context.SaveChangesAsync();
        }

        await using var adminContext = _fixture.Database.CreateDbContext();
        var admin = await CoreAccountSeeder.SeedAccountAsync(
            adminContext,
            CoreRoles.Admin,
            $"final-payment-admin-{Guid.NewGuid():N}@integration.test");

        using var request = IntegrationHttp.Authenticated(
            HttpMethod.Patch,
            $"/orders/{scenario.OrderId}/prepare-final-payment",
            admin.AccountId,
            CoreRoles.Admin);

        var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<FinalPaymentOrderScenario> SeedDeliveredWithRemainingAsync()
    {
        await using var context = _fixture.Database.CreateDbContext();
        return await FinalPaymentScenarioSeeder.SeedDeliveredOrderWithRemainingAsync(context);
    }

    private async Task<FinalPaymentOrderScenario> SeedDeliveredFullyPaidAsync()
    {
        await using var context = _fixture.Database.CreateDbContext();
        return await FinalPaymentScenarioSeeder.SeedDeliveredFullyPaidOrderAsync(context);
    }

    private async Task<OrderFinalPaymentPreparationDto> PrepareFinalPaymentAsync(
        FinalPaymentOrderScenario scenario)
    {
        await using var context = _fixture.Database.CreateDbContext();
        var admin = await CoreAccountSeeder.SeedAccountAsync(
            context,
            CoreRoles.Admin,
            $"final-payment-admin-{Guid.NewGuid():N}@integration.test");

        using var request = IntegrationHttp.Authenticated(
            HttpMethod.Patch,
            $"/orders/{scenario.OrderId}/prepare-final-payment",
            admin.AccountId,
            CoreRoles.Admin);

        var response = await _fixture.Client.SendAsync(request);
        return await ReadDataAsync<OrderFinalPaymentPreparationDto>(response, HttpStatusCode.OK);
    }

    private async Task<PaymentDetailDto> CreateRemainingPaymentAsync(FinalPaymentOrderScenario scenario)
    {
        using var request = BuildRemainingPaymentRequest(scenario);
        var response = await _fixture.Client.SendAsync(request);
        return await ReadDataAsync<PaymentDetailDto>(response, HttpStatusCode.Created);
    }

    private static HttpRequestMessage BuildRemainingPaymentRequest(FinalPaymentOrderScenario scenario)
    {
        return IntegrationHttp.AuthenticatedJson(
            HttpMethod.Post,
            $"/orders/{scenario.OrderId}/payments/remaining",
            scenario.SalesAccountId,
            CoreRoles.Sales,
            new CreateOrderRemainingPaymentRequestDto
            {
                Note = "Collect remaining payment"
            });
    }

    private async Task PayRemainingPaymentAsync(
        FinalPaymentOrderScenario scenario,
        Guid paymentId,
        decimal amount)
    {
        await CreatePayOsAttemptAsync(scenario.CustomerAccountId, paymentId);
        await ConfigureSuccessfulPayOsWebhookAsync(paymentId, amount);
        var response = await _fixture.Client.PostAsync(
            "/api/webhooks/payos",
            JsonContent.Create(new { payload = WebhookPayload }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task CreatePayOsAttemptAsync(Guid customerId, Guid paymentId)
    {
        using var request = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Post,
            $"/api/payments/{paymentId}/transactions",
            customerId,
            CoreRoles.Customer,
            new CreatePaymentTransactionAttemptRequestDto
            {
                PaymentProvider = PaymentProvider.PAYOS,
                PaymentMethod = PaymentMethod.PAYMENT_LINK,
                ReturnUrl = ReturnUrl,
                CancelUrl = CancelUrl
            });

        var response = await _fixture.Client.SendAsync(request);
        _ = await ReadDataAsync<PaymentTransactionAttemptResponseDto>(response, HttpStatusCode.OK);
    }

    private async Task ConfigureSuccessfulPayOsWebhookAsync(Guid paymentId, decimal amount)
    {
        await using var context = _fixture.Database.CreateDbContext();
        var transaction = await context.PaymentTransactionSet.SingleAsync(item => item.PaymentId == paymentId);
        var orderCode = long.Parse(transaction.ProviderReferenceCode!, CultureInfo.InvariantCulture);
        var fakePayOs = _fixture.Factory.Services.GetRequiredService<IPayOsClient>() as FakePayOsClient
            ?? throw new InvalidOperationException("Integration tests require FakePayOsClient.");
        fakePayOs.VerifiedWebhook = new PayOsVerifiedWebhookData
        {
            OrderCode = orderCode,
            Amount = (long)amount,
            Reference = WebhookReference,
            PaymentLinkId = transaction.ProviderTransactionId,
            TransactionDateTime = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            Code = "00"
        };
    }

    private async Task<OrderCompletionDto> CompleteOrderAsync(FinalPaymentOrderScenario scenario)
    {
        using var request = IntegrationHttp.Authenticated(
            HttpMethod.Patch,
            $"/orders/{scenario.OrderId}/complete",
            scenario.SalesAccountId,
            CoreRoles.Sales);

        var response = await _fixture.Client.SendAsync(request);
        return await ReadDataAsync<OrderCompletionDto>(response, HttpStatusCode.OK);
    }

    private async Task<ProjectCompletionDto> CompleteProjectAsync(FinalPaymentOrderScenario scenario)
    {
        using var request = IntegrationHttp.Authenticated(
            HttpMethod.Patch,
            $"/projects/{scenario.ProjectId}/complete",
            scenario.SalesAccountId,
            CoreRoles.Sales);

        var response = await _fixture.Client.SendAsync(request);
        return await ReadDataAsync<ProjectCompletionDto>(response, HttpStatusCode.OK);
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
