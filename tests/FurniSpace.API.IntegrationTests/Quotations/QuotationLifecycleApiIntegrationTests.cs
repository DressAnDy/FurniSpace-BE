using System.Net;
using System.Net.Http.Json;
using FurniSpace.API.IntegrationTests.Fixtures;
using FurniSpace.API.IntegrationTests.Support;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Quotations;
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
        Assert.Equal(10_000_000m, result.Data.TotalAmount);
        Assert.Single(result.Data.Items);

        var item = result.Data.Items[0];
        Assert.Equal(QuotationItemType.PRODUCT_ITEM, item.ItemType);
        Assert.Equal(scenario.ProposalItemId, item.ProposalItemId);
        Assert.Equal(scenario.ProductVersionId, item.ProductVersionId);
        Assert.Equal(10_000_000m, item.SubtotalAmount);

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

    private async Task<QuotationDraftScenario> SeedSelectedProposalAsync()
    {
        await using var context = _fixture.Database.CreateDbContext();
        return await QuotationAcceptScenarioSeeder.SeedSelectedProposalForQuotationAsync(context);
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
        var createResponse = await CreateDraftAsync(scenario);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var draft = await ReadQuotationAsync(createResponse);
        Assert.NotNull(draft.Data);

        var updateResponse = await UpdateValidUntilAsync(scenario.SalesAccountId, draft.Data.QuotationId);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        return draft.Data.QuotationId;
    }

    private async Task<HttpResponseMessage> UpdateValidUntilAsync(Guid salesId, Guid quotationId)
    {
        using var request = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Patch,
            $"/quotations/{quotationId}",
            salesId,
            CoreRoles.Sales,
            new UpdateQuotationRequestDto
            {
                ValidUntil = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7))
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
}
