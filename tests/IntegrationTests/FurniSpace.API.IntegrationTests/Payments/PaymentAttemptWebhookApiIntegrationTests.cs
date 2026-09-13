using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using FurniSpace.API.IntegrationTests.Fixtures;
using FurniSpace.API.IntegrationTests.Support;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Orders;
using FurniSpace.Application.DTOs.Payments;
using FurniSpace.Application.Interfaces.Payments;
using FurniSpace.Domain.Enums;
using FurniSpace.Testing.Fakes;
using FurniSpace.Testing.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FurniSpace.API.IntegrationTests.Payments;

[Collection(ApiIntegrationCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "Core")]
public sealed class PaymentAttemptWebhookApiIntegrationTests : IAsyncLifetime
{
    private const string ReturnUrl = "https://frontend.integration.test/payments/return";
    private const string CancelUrl = "https://frontend.integration.test/payments/cancel";
    private const string WebhookReference = "PAYOS-REF-INTEGRATION";
    private const string WebhookPayload = "{}";

    private readonly ApiIntegrationFixture _fixture;

    public PaymentAttemptWebhookApiIntegrationTests(ApiIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.Database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreatePayOsAttempt_OnPendingDeposit_ReturnsProcessingTransaction()
    {
        var scenario = await SeedDepositOrderAsync();
        var paymentId = await CreateDepositPaymentAsync(scenario);

        var attempt = await CreatePayOsAttemptAsync(scenario.CustomerAccountId, paymentId);

        Assert.Equal(PaymentTransactionStatus.PENDING, attempt.Status);
        Assert.Equal(PaymentProvider.PAYOS, attempt.PaymentProvider);
        Assert.Equal(PaymentMethod.PAYMENT_LINK, attempt.PaymentMethod);
        Assert.Equal(PaymentStatus.PROCESSING, attempt.PaymentStatus);
        Assert.Contains("https://payos.integration.test/", attempt.PaymentUrl, StringComparison.Ordinal);

        await using var context = _fixture.Database.CreateDbContext();
        var payment = await context.PaymentSet.SingleAsync(item => item.PaymentId == paymentId);
        var transaction = await context.PaymentTransactionSet.SingleAsync(item => item.PaymentId == paymentId);
        Assert.Equal(PaymentStatus.PROCESSING, payment.Status);
        Assert.Equal(PaymentTransactionStatus.PENDING, transaction.Status);
        Assert.False(string.IsNullOrWhiteSpace(transaction.ProviderReferenceCode));
    }

    [Fact]
    public async Task PayOsWebhookSuccess_MarksPaymentPaidAndAppliesDepositOrderEffect()
    {
        var scenario = await SeedDepositOrderAsync();
        var paymentId = await CreateDepositPaymentAsync(scenario);
        await CreatePayOsAttemptAsync(scenario.CustomerAccountId, paymentId);
        await ConfigureSuccessfulPayOsWebhookAsync(paymentId, scenario.DepositAmount);

        var response = await PostPayOsWebhookAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var context = _fixture.Database.CreateDbContext();
        var payment = await context.PaymentSet.SingleAsync(item => item.PaymentId == paymentId);
        var transaction = await context.PaymentTransactionSet.SingleAsync(item => item.PaymentId == paymentId);
        var order = await context.OrderSet.SingleAsync(item => item.OrderId == scenario.OrderId);
        Assert.Equal(PaymentStatus.PAID, payment.Status);
        Assert.Equal(PaymentTransactionStatus.SUCCESS, transaction.Status);
        Assert.Equal(OrderStatus.DEPOSIT_PAID, order.Status);
        Assert.Equal(scenario.DepositAmount, order.PaidAmount);
        Assert.Equal(7_800_000m, order.RemainingAmount);
    }

    [Fact]
    public async Task PayOsWebhookSuccess_WhenPostedTwice_IsIdempotent()
    {
        var scenario = await SeedDepositOrderAsync();
        var paymentId = await CreateDepositPaymentAsync(scenario);
        await CreatePayOsAttemptAsync(scenario.CustomerAccountId, paymentId);
        await ConfigureSuccessfulPayOsWebhookAsync(paymentId, scenario.DepositAmount);

        var firstResponse = await PostPayOsWebhookAsync();
        var duplicateResponse = await PostPayOsWebhookAsync();

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, duplicateResponse.StatusCode);
        await using var context = _fixture.Database.CreateDbContext();
        Assert.Equal(1, await context.PaymentTransactionSet.CountAsync(
            transaction => transaction.Status == PaymentTransactionStatus.SUCCESS));
        var order = await context.OrderSet.SingleAsync(item => item.OrderId == scenario.OrderId);
        Assert.Equal(scenario.DepositAmount, order.PaidAmount);
        Assert.Equal(7_800_000m, order.RemainingAmount);
    }

    [Fact]
    public async Task CancelAttempt_WhenPending_RevertsPaymentToPending()
    {
        var scenario = await SeedDepositOrderAsync();
        var paymentId = await CreateDepositPaymentAsync(scenario);
        var attempt = await CreatePayOsAttemptAsync(scenario.CustomerAccountId, paymentId);

        using var request = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Patch,
            $"/api/payments/{paymentId}/transactions/{attempt.PaymentTransactionId}/cancel",
            scenario.CustomerAccountId,
            CoreRoles.Customer,
            new CancelPaymentTransactionRequestDto { CancelReason = "Customer cancelled checkout." });

        var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var context = _fixture.Database.CreateDbContext();
        var payment = await context.PaymentSet.SingleAsync(item => item.PaymentId == paymentId);
        var transaction = await context.PaymentTransactionSet.SingleAsync(item => item.PaymentId == paymentId);
        Assert.Equal(PaymentStatus.PENDING, payment.Status);
        Assert.Equal(PaymentTransactionStatus.CANCELLED, transaction.Status);
        Assert.Equal("Customer cancelled checkout.", transaction.FailureReason);
    }

    [Fact]
    public async Task CreateAttempt_WhenPaymentExpired_ReturnsBadRequestAndMarksExpired()
    {
        var scenario = await SeedDepositOrderAsync();
        var paymentId = await CreateDepositPaymentAsync(
            scenario,
            expiredAt: DateTime.UtcNow.AddMinutes(-5));

        using var request = CreatePayOsAttemptRequest(scenario.CustomerAccountId, paymentId);
        var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var context = _fixture.Database.CreateDbContext();
        var payment = await context.PaymentSet.SingleAsync(item => item.PaymentId == paymentId);
        Assert.Equal(PaymentStatus.EXPIRED, payment.Status);
        Assert.Equal(0, await context.PaymentTransactionSet.CountAsync());
    }

    [Fact]
    public async Task CreateAttempt_WhenDifferentCustomerCalls_ReturnsForbidden()
    {
        var scenario = await SeedDepositOrderAsync();
        var otherCustomer = await SeedOtherCustomerAsync();
        var paymentId = await CreateDepositPaymentAsync(scenario);

        using var request = CreatePayOsAttemptRequest(otherCustomer.AccountId, paymentId);
        var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var context = _fixture.Database.CreateDbContext();
        Assert.Equal(0, await context.PaymentTransactionSet.CountAsync());
    }

    private async Task<DepositOrderScenario> SeedDepositOrderAsync()
    {
        await using var context = _fixture.Database.CreateDbContext();
        return await DepositOrderScenarioSeeder.SeedDepositPendingOrderAsync(context);
    }

    private async Task<SeededAccount> SeedOtherCustomerAsync()
    {
        await using var context = _fixture.Database.CreateDbContext();
        return await CoreAccountSeeder.SeedAccountAsync(
            context,
            CoreRoles.Customer,
            $"payment-other-customer-{Guid.NewGuid():N}@integration.test");
    }

    private async Task<Guid> CreateDepositPaymentAsync(
        DepositOrderScenario scenario,
        DateTime? expiredAt = null)
    {
        using var request = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Post,
            $"/orders/{scenario.OrderId}/payments/deposit",
            scenario.CustomerAccountId,
            CoreRoles.Customer,
            new CreateOrderDepositPaymentRequestDto { ExpiredAt = expiredAt });

        var response = await _fixture.Client.SendAsync(request);
        var result = await response.Content
            .ReadFromJsonAsync<ServiceResult<PaymentDetailDto>>(IntegrationHttp.JsonOptions);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(result?.Data);
        return result.Data.PaymentId;
    }

    private async Task<PaymentTransactionAttemptResponseDto> CreatePayOsAttemptAsync(
        Guid customerId,
        Guid paymentId)
    {
        using var request = CreatePayOsAttemptRequest(customerId, paymentId);
        var response = await _fixture.Client.SendAsync(request);
        var result = await response.Content
            .ReadFromJsonAsync<ServiceResult<PaymentTransactionAttemptResponseDto>>(IntegrationHttp.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result?.Data);
        return result.Data;
    }

    private static HttpRequestMessage CreatePayOsAttemptRequest(Guid customerId, Guid paymentId)
    {
        return IntegrationHttp.AuthenticatedJson(
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

    private async Task<HttpResponseMessage> PostPayOsWebhookAsync()
    {
        var response = await _fixture.Client.PostAsync(
            "/api/webhooks/payos",
            JsonContent.Create(new { payload = WebhookPayload }));
        return response;
    }
}
