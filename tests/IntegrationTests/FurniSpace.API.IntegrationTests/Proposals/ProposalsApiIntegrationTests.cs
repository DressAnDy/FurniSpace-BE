using System.Net;
using System.Net.Http.Json;
using FurniSpace.API.IntegrationTests.Fixtures;
using FurniSpace.API.IntegrationTests.Support;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Proposals;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Testing.Seeding;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.API.IntegrationTests.Proposals;

[Collection(ApiIntegrationCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "Core")]
public sealed class ProposalsApiIntegrationTests : IAsyncLifetime
{
    private readonly ApiIntegrationFixture _fixture;

    public ProposalsApiIntegrationTests(ApiIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.Database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreatePublishAndSelectFinal_MovesProjectAndRejectsOtherProposals()
    {
        ProposalConsultingScenario scenario;
        await using (var context = _fixture.Database.CreateDbContext())
        {
            scenario = await ProposalScenarioSeeder.SeedProposalConsultingAsync(context);
            context.ProposalSet.Add(ProposalScenarioSeeder.CreateProposal(
                scenario.ProjectId,
                scenario.DesignerAccountId,
                "Other Draft",
                ProposalStatus.DRAFT,
                versionNo: 1));
            await context.SaveChangesAsync();
        }

        using var createRequest = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Post,
            $"/projects/{scenario.ProjectId}/proposals",
            scenario.DesignerAccountId,
            CoreRoles.Designer,
            new CreateProposalRequestDto
            {
                ProposalName = " Main Option ",
                Description = " Primary layout "
            });
        var createResponse = await _fixture.Client.SendAsync(createRequest);
        var created = await createResponse.Content
            .ReadFromJsonAsync<ServiceResult<ProposalDto>>(IntegrationHttp.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.Equal(ProposalStatus.DRAFT, created?.Data?.Status);
        Assert.Equal("Main Option", created?.Data?.ProposalName);
        Assert.NotNull(created?.Data);

        using var sceneRequest = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Post,
            $"/proposals/{created.Data.ProposalId}/scenes",
            scenario.DesignerAccountId,
            CoreRoles.Designer,
            new CreateProposalSceneRequestDto
            {
                SceneName = "Main Layout",
                SceneType = ProposalSceneType.ROOM_PLANNER,
                ProjectAreaIds = [scenario.FloorAreaId]
            });
        var sceneResponse = await _fixture.Client.SendAsync(sceneRequest);
        Assert.Equal(HttpStatusCode.Created, sceneResponse.StatusCode);

        using var publishRequest = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Patch,
            $"/proposals/{created.Data.ProposalId}/publish",
            scenario.DesignerAccountId,
            CoreRoles.Designer,
            new PublishProposalRequestDto());
        var publishResponse = await _fixture.Client.SendAsync(publishRequest);
        var published = await publishResponse.Content
            .ReadFromJsonAsync<ServiceResult<PublishProposalResponseDto>>(IntegrationHttp.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);
        Assert.Equal(ProposalStatus.PUBLISHED, published?.Data?.ProposalStatus);

        using var selectRequest = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Patch,
            $"/proposals/{created.Data.ProposalId}/select-final",
            scenario.CustomerAccountId,
            CoreRoles.Customer,
            new SelectFinalProposalRequestDto());
        var selectResponse = await _fixture.Client.SendAsync(selectRequest);
        var selected = await selectResponse.Content
            .ReadFromJsonAsync<ServiceResult<SelectFinalProposalResponseDto>>(IntegrationHttp.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, selectResponse.StatusCode);
        Assert.Equal(ProposalStatus.SELECTED, selected?.Data?.ProposalStatus);
        Assert.Equal(ProjectStatus.PROPOSAL_SELECTED, selected?.Data?.ProjectStatus);

        await using var verification = _fixture.Database.CreateDbContext();
        var selectedProposal = await verification.ProposalSet.SingleAsync(p => p.ProposalName == "Main Option");
        var otherProposal = await verification.ProposalSet.SingleAsync(p => p.ProposalName == "Other Draft");
        Assert.Equal(ProposalStatus.SELECTED, selectedProposal.Status);
        Assert.Equal(ProposalStatus.REJECTED, otherProposal.Status);
        var project = await verification.ProjectSet.SingleAsync();
        Assert.Equal(ProjectStatus.PROPOSAL_SELECTED, project.Status);
    }

    [Fact]
    public async Task CustomerList_SeesOnlyPublishedProposals()
    {
        ProposalConsultingScenario scenario;
        await using (var context = _fixture.Database.CreateDbContext())
        {
            scenario = await ProposalScenarioSeeder.SeedProposalConsultingAsync(context);
            context.ProposalSet.AddRange(
                ProposalScenarioSeeder.CreateProposal(
                    scenario.ProjectId,
                    scenario.DesignerAccountId,
                    "Draft Hidden",
                    ProposalStatus.DRAFT),
                ProposalScenarioSeeder.CreateProposal(
                    scenario.ProjectId,
                    scenario.DesignerAccountId,
                    "Published Visible",
                    ProposalStatus.PUBLISHED));
            await context.SaveChangesAsync();
        }

        using var request = IntegrationHttp.Authenticated(
            HttpMethod.Get,
            $"/projects/{scenario.ProjectId}/proposals",
            scenario.CustomerAccountId,
            CoreRoles.Customer);
        var response = await _fixture.Client.SendAsync(request);
        var result = await response.Content
            .ReadFromJsonAsync<ServiceResult<ProposalListResponseDto>>(IntegrationHttp.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result?.Data);
        Assert.Equal(1, result.Data.Total);
        Assert.Equal("Published Visible", result.Data.Items.Single().ProposalName);
    }

    [Fact]
    public async Task RequestRevision_AsOwnerCustomer_MovesToRevisionRequested()
    {
        PublishedProposalScenario scenario;
        await using (var context = _fixture.Database.CreateDbContext())
        {
            scenario = await ProposalScenarioSeeder.SeedPublishedProposalAsync(context);
        }

        using var request = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Patch,
            $"/proposals/{scenario.ProposalId}/request-revision",
            scenario.CustomerAccountId,
            CoreRoles.Customer,
            new RequestProposalRevisionRequestDto { RevisionNote = " Please revise lighting. " });

        var response = await _fixture.Client.SendAsync(request);
        var result = await response.Content
            .ReadFromJsonAsync<ServiceResult<RequestProposalRevisionResponseDto>>(IntegrationHttp.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ProposalStatus.REVISION_REQUESTED, result?.Data?.ProposalStatus);
        Assert.Equal("Please revise lighting.", result?.Data?.RevisionNote);

        await using var verification = _fixture.Database.CreateDbContext();
        var proposal = await verification.ProposalSet.SingleAsync(p => p.ProposalId == scenario.ProposalId);
        Assert.Equal(ProposalStatus.REVISION_REQUESTED, proposal.Status);
    }

    [Fact]
    public async Task SelectFinal_WhenPendingCustomizationExists_ReturnsBadRequest()
    {
        PublishedProposalScenario scenario;
        await using (var context = _fixture.Database.CreateDbContext())
        {
            scenario = await ProposalScenarioSeeder.SeedPublishedProposalAsync(context);
            context.CustomizationRequestSet.Add(new CustomizationRequest
            {
                CustomizationRequestId = Guid.NewGuid(),
                ProjectId = scenario.ProjectId,
                ProposalId = scenario.ProposalId,
                SourceProductVersionId = scenario.ProductVersionId,
                RequestedByCustomerId = scenario.CustomerAccountId,
                RequestTitle = "Pending change",
                RequestedMaterial = "Walnut",
                Status = CustomizationStatus.SUBMITTED,
                CreatedAt = CoreAccountSeeder.FixedTimestamp,
                UpdatedAt = CoreAccountSeeder.FixedTimestamp
            });
            await context.SaveChangesAsync();
        }

        using var request = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Patch,
            $"/proposals/{scenario.ProposalId}/select-final",
            scenario.CustomerAccountId,
            CoreRoles.Customer,
            new SelectFinalProposalRequestDto());

        var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var verification = _fixture.Database.CreateDbContext();
        var proposal = await verification.ProposalSet.SingleAsync(p => p.ProposalId == scenario.ProposalId);
        Assert.Equal(ProposalStatus.PUBLISHED, proposal.Status);
        var project = await verification.ProjectSet.SingleAsync();
        Assert.Equal(ProjectStatus.PROPOSAL_CONSULTING, project.Status);
    }
}
