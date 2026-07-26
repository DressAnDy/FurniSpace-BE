using System.Net;
using System.Net.Http.Json;
using FurniSpace.API.IntegrationTests.Fixtures;
using FurniSpace.API.IntegrationTests.Support;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Payments;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Testing.Seeding;

namespace FurniSpace.API.IntegrationTests.Payments;

[Collection(ApiIntegrationCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "Core")]
public sealed class PaymentsApiIntegrationTests : IAsyncLifetime
{
    private readonly ApiIntegrationFixture _fixture;

    public PaymentsApiIntegrationTests(ApiIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.Database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetStatusByCode_ReturnsPersistedPaymentStateForProjectCustomer()
    {
        SeededAccount customer;
        string paymentCode;
        await using (var context = _fixture.Database.CreateDbContext())
        {
            customer = await CoreAccountSeeder.SeedAccountAsync(
                context,
                CoreRoles.Customer,
                "payment-customer@integration.test");

            var project = ProjectScenarioSeeder.CreateProject(
                customer.AccountId,
                assignedSalesId: null,
                "PRJ-2026-0002",
                "Payment Project",
                ProjectStatus.ORDER_CONFIRMED);
            paymentCode = "PAY-INTEGRATION-001";
            context.ProjectSet.Add(project);
            context.PaymentSet.Add(new Payment
            {
                PaymentId = Guid.NewGuid(),
                ProjectId = project.ProjectId,
                PaymentCode = paymentCode,
                PaidBy = customer.AccountId,
                PaymentType = PaymentType.DEPOSIT,
                Amount = 25_000_000m,
                PaidAmount = 10_000_000m,
                RemainingAmount = 15_000_000m,
                Currency = "VND",
                Status = PaymentStatus.PARTIALLY_PAID
            });
            await context.SaveChangesAsync();
        }

        using var request = IntegrationHttp.Authenticated(
            HttpMethod.Get,
            $"/api/payments/code/{paymentCode}/status",
            customer.AccountId,
            CoreRoles.Customer);

        var response = await _fixture.Client.SendAsync(request);
        var result = await response.Content
            .ReadFromJsonAsync<ServiceResult<PaymentStatusByCodeDto>>(IntegrationHttp.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result?.Data);
        Assert.Equal(PaymentStatus.PARTIALLY_PAID, result.Data.Status);
        Assert.Equal(25_000_000m, result.Data.Amount);
        Assert.Equal(10_000_000m, result.Data.PaidAmount);
        Assert.Equal(15_000_000m, result.Data.RemainingAmount);
    }

    [Fact]
    public async Task GetStatusByCode_WhenPaymentDoesNotExist_ReturnsNotFound()
    {
        SeededAccount customer;
        await using (var context = _fixture.Database.CreateDbContext())
        {
            customer = await CoreAccountSeeder.SeedAccountAsync(context, CoreRoles.Customer);
        }

        using var request = IntegrationHttp.Authenticated(
            HttpMethod.Get,
            "/api/payments/code/PAY-DOES-NOT-EXIST/status",
            customer.AccountId,
            CoreRoles.Customer);

        var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
