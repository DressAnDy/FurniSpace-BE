using System.Net;
using System.Net.Http.Json;
using FurniSpace.API.IntegrationTests.Fixtures;
using FurniSpace.API.IntegrationTests.Support;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Payments;
using FurniSpace.Domain.Enums;
using FurniSpace.Testing.Seeding;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.API.IntegrationTests.Payments;

[Collection(ApiIntegrationCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "Core")]
public sealed class ProjectStartFeeApiIntegrationTests : IAsyncLifetime
{
    private readonly ApiIntegrationFixture _fixture;

    public ProjectStartFeeApiIntegrationTests(ApiIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.Database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateProjectStartFee_AsAssignedSales_PersistsPendingObligation()
    {
        ProjectConsultationScenario scenario;
        await using (var context = _fixture.Database.CreateDbContext())
        {
            scenario = await ProjectScenarioSeeder.SeedInConsultationAsync(context);
        }

        using var request = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Post,
            $"/api/projects/{scenario.ProjectId}/payments/project-start-fee",
            scenario.SalesAccountId,
            CoreRoles.Sales,
            new CreateProjectStartFeePaymentRequestDto
            {
                Amount = 2_500_000m,
                Note = "Project start fee"
            });

        var response = await _fixture.Client.SendAsync(request);
        var result = await response.Content
            .ReadFromJsonAsync<ServiceResult<PaymentDetailDto>>(IntegrationHttp.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(result?.Data);
        Assert.Equal(PaymentType.PROJECT_START_FEE, result.Data.PaymentType);
        Assert.Equal(PaymentStatus.PENDING, result.Data.Status);
        Assert.Equal(2_500_000m, result.Data.Amount);
        Assert.Equal(scenario.ProjectId, result.Data.ProjectId);
        Assert.Equal(scenario.CustomerAccountId, result.Data.PaidBy);

        await using var verification = _fixture.Database.CreateDbContext();
        var payment = await verification.PaymentSet.SingleAsync();
        Assert.Equal(PaymentType.PROJECT_START_FEE, payment.PaymentType);
        Assert.Equal(PaymentStatus.PENDING, payment.Status);
        Assert.Equal(2_500_000m, payment.Amount);
        Assert.Null(payment.OrderId);
    }

    [Fact]
    public async Task CreateProjectStartFee_Twice_ReturnsSameActivePayment()
    {
        ProjectConsultationScenario scenario;
        await using (var context = _fixture.Database.CreateDbContext())
        {
            scenario = await ProjectScenarioSeeder.SeedInConsultationAsync(context);
        }

        using var firstRequest = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Post,
            $"/api/projects/{scenario.ProjectId}/payments/project-start-fee",
            scenario.SalesAccountId,
            CoreRoles.Sales,
            new CreateProjectStartFeePaymentRequestDto());
        var firstResponse = await _fixture.Client.SendAsync(firstRequest);
        var first = await firstResponse.Content
            .ReadFromJsonAsync<ServiceResult<PaymentDetailDto>>(IntegrationHttp.JsonOptions);

        using var secondRequest = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Post,
            $"/api/projects/{scenario.ProjectId}/payments/project-start-fee",
            scenario.SalesAccountId,
            CoreRoles.Sales,
            new CreateProjectStartFeePaymentRequestDto());
        var secondResponse = await _fixture.Client.SendAsync(secondRequest);
        var second = await secondResponse.Content
            .ReadFromJsonAsync<ServiceResult<PaymentDetailDto>>(IntegrationHttp.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Equal(first?.Data?.PaymentId, second?.Data?.PaymentId);

        await using var verification = _fixture.Database.CreateDbContext();
        Assert.Equal(1, await verification.PaymentSet.CountAsync());
    }

    [Fact]
    public async Task GetProjectStartFeeStatus_ReturnsEligibilityForAssignedSales()
    {
        ProjectConsultationScenario scenario;
        await using (var context = _fixture.Database.CreateDbContext())
        {
            scenario = await ProjectScenarioSeeder.SeedInConsultationAsync(context);
            context.PaymentSet.Add(ProjectScenarioSeeder.CreatePaidProjectStartFee(
                scenario.ProjectId,
                scenario.CustomerAccountId,
                Guid.NewGuid().ToString("N")[..8]));
            await context.SaveChangesAsync();
        }

        using var request = IntegrationHttp.Authenticated(
            HttpMethod.Get,
            $"/api/projects/{scenario.ProjectId}/payments/project-start-fee/status",
            scenario.SalesAccountId,
            CoreRoles.Sales);

        var response = await _fixture.Client.SendAsync(request);
        var result = await response.Content
            .ReadFromJsonAsync<ServiceResult<ProjectStartFeeStatusDto>>(IntegrationHttp.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result?.Data);
        Assert.True(result.Data.RequiresProjectStartFee);
        Assert.Equal(PaymentStatus.PAID, result.Data.ProjectStartFeeStatus);
        Assert.True(result.Data.IsEligibleForDesignerAssignment);
    }

    [Fact]
    public async Task CreateProjectStartFee_AsCustomer_ReturnsForbidden()
    {
        ProjectConsultationScenario scenario;
        await using (var context = _fixture.Database.CreateDbContext())
        {
            scenario = await ProjectScenarioSeeder.SeedInConsultationAsync(context);
        }

        using var request = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Post,
            $"/api/projects/{scenario.ProjectId}/payments/project-start-fee",
            scenario.CustomerAccountId,
            CoreRoles.Customer,
            new CreateProjectStartFeePaymentRequestDto());

        var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var verification = _fixture.Database.CreateDbContext();
        Assert.Equal(0, await verification.PaymentSet.CountAsync());
    }
}
