using System.Net;
using FurniSpace.API.IntegrationTests.Fixtures;
using FurniSpace.API.IntegrationTests.Support;
using FurniSpace.Domain.Enums;
using FurniSpace.Testing.Seeding;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.API.IntegrationTests.Quotations;

[Collection(ApiIntegrationCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "Core")]
public sealed class QuotationAcceptanceApiIntegrationTests : IAsyncLifetime
{
    private readonly ApiIntegrationFixture _fixture;

    public QuotationAcceptanceApiIntegrationTests(ApiIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.Database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Accept_CreatesOrderAndMovesProjectAtomically()
    {
        QuotationAcceptScenario scenario;
        await using (var context = _fixture.Database.CreateDbContext())
        {
            scenario = await QuotationAcceptScenarioSeeder.SeedSentQuotationAsync(context);
        }

        using var request = IntegrationHttp.Authenticated(
            HttpMethod.Patch,
            $"/quotations/{scenario.QuotationId}/accept",
            scenario.CustomerAccountId,
            CoreRoles.Customer);

        var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var verification = _fixture.Database.CreateDbContext();
        var quotation = await verification.QuotationSet.SingleAsync();
        var project = await verification.ProjectSet.SingleAsync();
        var order = await verification.OrderSet.SingleAsync();
        var orderItem = await verification.OrderItemSet.SingleAsync();

        Assert.Equal(QuotationStatus.ACCEPTED, quotation.Status);
        Assert.Equal(ProjectStatus.ORDER_CONFIRMED, project.Status);
        Assert.Equal(quotation.QuotationId, order.QuotationId);
        Assert.Equal(OrderStatus.DEPOSIT_PENDING, order.Status);
        Assert.Equal(10_000_000m, order.FinalTotalAmount);
        Assert.Equal(order.OrderId, orderItem.OrderId);
        Assert.Equal("Design service", orderItem.ProductNameSnapshot);
    }

    [Fact]
    public async Task Accept_WhenQuotationAlreadyAccepted_ReturnsBadRequest()
    {
        QuotationAcceptScenario scenario;
        await using (var context = _fixture.Database.CreateDbContext())
        {
            scenario = await QuotationAcceptScenarioSeeder.SeedSentQuotationAsync(context);
            var quotation = await context.QuotationSet.SingleAsync();
            quotation.Status = QuotationStatus.ACCEPTED;
            quotation.AcceptedAt = CoreAccountSeeder.FixedTimestamp;
            await context.SaveChangesAsync();
        }

        using var request = IntegrationHttp.Authenticated(
            HttpMethod.Patch,
            $"/quotations/{scenario.QuotationId}/accept",
            scenario.CustomerAccountId,
            CoreRoles.Customer);

        var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var verification = _fixture.Database.CreateDbContext();
        Assert.Equal(0, await verification.OrderSet.CountAsync());
    }
}
