using System.Net;
using System.Net.Http.Json;
using FurniSpace.API.IntegrationTests.Fixtures;
using FurniSpace.API.IntegrationTests.Support;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Orders;
using FurniSpace.Application.DTOs.Payments;
using FurniSpace.Domain.Enums;
using FurniSpace.Testing.Seeding;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.API.IntegrationTests.Payments;

[Collection(ApiIntegrationCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "Core")]
public sealed class OrderDepositPaymentApiIntegrationTests : IAsyncLifetime
{
    private readonly ApiIntegrationFixture _fixture;

    public OrderDepositPaymentApiIntegrationTests(ApiIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.Database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateDeposit_ThenGetById_PersistsPendingPayment()
    {
        DepositOrderScenario scenario;
        await using (var context = _fixture.Database.CreateDbContext())
        {
            scenario = await DepositOrderScenarioSeeder.SeedDepositPendingOrderAsync(context);
        }

        using var createRequest = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Post,
            $"/orders/{scenario.OrderId}/payments/deposit",
            scenario.CustomerAccountId,
            CoreRoles.Customer,
            new CreateOrderDepositPaymentRequestDto { Note = "Initial deposit" });

        var createResponse = await _fixture.Client.SendAsync(createRequest);
        var created = await createResponse.Content
            .ReadFromJsonAsync<ServiceResult<PaymentDetailDto>>(IntegrationHttp.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(created?.Data);
        Assert.Equal(PaymentType.DEPOSIT, created.Data.PaymentType);
        Assert.Equal(PaymentStatus.PENDING, created.Data.Status);
        Assert.Equal(scenario.DepositAmount, created.Data.Amount);
        Assert.Equal(scenario.OrderId, created.Data.OrderId);

        using var getRequest = IntegrationHttp.Authenticated(
            HttpMethod.Get,
            $"/api/payments/{created.Data.PaymentId}",
            scenario.CustomerAccountId,
            CoreRoles.Customer);
        var getResponse = await _fixture.Client.SendAsync(getRequest);
        var detail = await getResponse.Content
            .ReadFromJsonAsync<ServiceResult<PaymentDetailDto>>(IntegrationHttp.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(created.Data.PaymentId, detail?.Data?.PaymentId);
        Assert.Equal(created.Data.PaymentCode, detail?.Data?.PaymentCode);

        await using var verification = _fixture.Database.CreateDbContext();
        var payment = await verification.PaymentSet.SingleAsync();
        Assert.Equal(PaymentStatus.PENDING, payment.Status);
        Assert.Equal(scenario.DepositAmount, payment.RemainingAmount);
    }

    [Fact]
    public async Task CreateDeposit_WhenOrderDoesNotExist_ReturnsNotFound()
    {
        SeededAccount customer;
        await using (var context = _fixture.Database.CreateDbContext())
        {
            customer = await CoreAccountSeeder.SeedAccountAsync(context, CoreRoles.Customer);
        }

        using var request = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Post,
            $"/orders/{Guid.NewGuid()}/payments/deposit",
            customer.AccountId,
            CoreRoles.Customer,
            new CreateOrderDepositPaymentRequestDto());

        var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
