using System.Net;
using System.Net.Http.Json;
using FurniSpace.API.IntegrationTests.Fixtures;
using FurniSpace.API.IntegrationTests.Support;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.CustomizationRequests;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Testing.Seeding;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.API.IntegrationTests.CustomizationRequests;

[Collection(ApiIntegrationCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "Core")]
public sealed class CustomizationRequestsApiIntegrationTests : IAsyncLifetime
{
    private readonly ApiIntegrationFixture _fixture;

    public CustomizationRequestsApiIntegrationTests(ApiIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.Database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task HappyPath_CreateVersionReviewAccept_WithdrawsOtherVersions()
    {
        CustomizationReadyScenario scenario;
        await using (var context = _fixture.Database.CreateDbContext())
        {
            scenario = await CustomizationScenarioSeeder.SeedPublishedItemAsync(context);
        }

        using var submitRequest = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Post,
            $"/proposal-items/{scenario.ProposalItemId}/customization-requests",
            scenario.CustomerAccountId,
            CoreRoles.Customer,
            new SubmitCustomizationRequestDto
            {
                RequestTitle = "Change desk material",
                RequestedMaterial = "Walnut",
                RequestedColor = "Dark brown"
            });
        var submitResponse = await _fixture.Client.SendAsync(submitRequest);
        var submitted = await submitResponse.Content
            .ReadFromJsonAsync<ServiceResult<CustomizationRequestDto>>(IntegrationHttp.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, submitResponse.StatusCode);
        Assert.Equal(CustomizationStatus.SUBMITTED, submitted?.Data?.Status);
        Assert.NotNull(submitted?.Data);
        var requestId = submitted.Data.CustomizationRequestId;

        using var version1Request = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Post,
            $"/customization-requests/{requestId}/versions",
            scenario.DesignerAccountId,
            CoreRoles.Designer,
            new CreateCustomizationRequestVersionDto
            {
                VersionName = "Custom Desk V1",
                DimensionUnit = "cm",
                Material = "Walnut",
                Color = "Dark brown",
                Width = 140,
                Height = 75,
                Depth = 60,
                EstimatedPrice = 6_500_000m
            });
        var version1Response = await _fixture.Client.SendAsync(version1Request);
        var version1 = await version1Response.Content
            .ReadFromJsonAsync<ServiceResult<CreateCustomizationRequestVersionResponseDto>>(IntegrationHttp.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, version1Response.StatusCode);
        Assert.Equal(CustomizationVersionStatus.DRAFT, version1?.Data?.Version.Status);
        Assert.NotNull(version1?.Data);

        using var version2Request = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Post,
            $"/customization-requests/{requestId}/versions",
            scenario.DesignerAccountId,
            CoreRoles.Designer,
            new CreateCustomizationRequestVersionDto
            {
                VersionName = "Custom Desk V2",
                DimensionUnit = "cm",
                Material = "Teak"
            });
        var version2Response = await _fixture.Client.SendAsync(version2Request);
        var version2 = await version2Response.Content
            .ReadFromJsonAsync<ServiceResult<CreateCustomizationRequestVersionResponseDto>>(IntegrationHttp.JsonOptions);
        Assert.Equal(HttpStatusCode.Created, version2Response.StatusCode);
        Assert.NotNull(version2?.Data);

        using var submitV1 = IntegrationHttp.Authenticated(
            HttpMethod.Post,
            $"/customization-requests/{requestId}/versions/{version1.Data.CustomizationRequestVersionId}/submit-for-review",
            scenario.DesignerAccountId,
            CoreRoles.Designer);
        var submitV1Response = await _fixture.Client.SendAsync(submitV1);
        Assert.Equal(HttpStatusCode.OK, submitV1Response.StatusCode);

        using var reviewRequest = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Patch,
            $"/api/production/customization-versions/{version1.Data.CustomizationRequestVersionId}/review",
            scenario.ProductionAccountId,
            CoreRoles.Production,
            new ReviewCustomizationVersionDto
            {
                Result = "FEASIBLE",
                MaterialAvailable = true,
                EstimatedProductionDays = 5,
                EstimatedAdditionalCost = 1_500_000m,
                AdditionalCostReason = "Custom finish",
                FeasibilityNote = "OK"
            });
        var reviewResponse = await _fixture.Client.SendAsync(reviewRequest);
        var reviewed = await reviewResponse.Content
            .ReadFromJsonAsync<ServiceResult<ProductionCustomizationVersionDetailDto>>(IntegrationHttp.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, reviewResponse.StatusCode);
        Assert.Equal(ProductionFeasibilityStatus.FEASIBLE, reviewed?.Data?.Version.FeasibilityStatus);

        using var acceptRequest = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Post,
            $"/customization-requests/{requestId}/accept",
            scenario.CustomerAccountId,
            CoreRoles.Customer,
            new AcceptCustomizationRequestDto
            {
                CustomizationRequestVersionId = version1.Data.CustomizationRequestVersionId
            });
        var acceptResponse = await _fixture.Client.SendAsync(acceptRequest);
        var accepted = await acceptResponse.Content
            .ReadFromJsonAsync<ServiceResult<CustomizationRequestDto>>(IntegrationHttp.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);
        Assert.Equal(CustomizationStatus.ACCEPTED, accepted?.Data?.Status);
        Assert.Equal(version1.Data.CustomizationRequestVersionId, accepted?.Data?.AcceptedRequestVersionId);

        await using var verification = _fixture.Database.CreateDbContext();
        var request = await verification.CustomizationRequestSet.SingleAsync();
        Assert.Equal(CustomizationStatus.ACCEPTED, request.Status);
        Assert.Equal(version1.Data.CustomizationRequestVersionId, request.AcceptedRequestVersionId);

        var versions = await verification.CustomizationRequestVersionSet
            .OrderBy(v => v.VersionNo)
            .ToListAsync();
        Assert.Equal(2, versions.Count);
        Assert.Equal(CustomizationVersionStatus.ACCEPTED, versions[0].Status);
        Assert.Equal(CustomizationVersionStatus.WITHDRAWN, versions[1].Status);
        Assert.Equal(2, await verification.ProductVersionSet.CountAsync(v => v.IsProjectSpecific == true));
    }

    [Fact]
    public async Task CreateRequest_AsUnassignedDesigner_ReturnsForbidden()
    {
        CustomizationReadyScenario scenario;
        await using (var context = _fixture.Database.CreateDbContext())
        {
            scenario = await CustomizationScenarioSeeder.SeedPublishedItemAsync(context);
        }

        using var request = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Post,
            $"/proposal-items/{scenario.ProposalItemId}/customization-requests",
            scenario.UnassignedDesignerAccountId,
            CoreRoles.Designer,
            new SubmitCustomizationRequestDto
            {
                RequestTitle = "Unauthorized",
                RequestedMaterial = "Walnut"
            });

        var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var verification = _fixture.Database.CreateDbContext();
        Assert.Equal(0, await verification.CustomizationRequestSet.CountAsync());
    }

    [Fact]
    public async Task ProductionReview_NotFeasible_RejectsVersionWithoutCancellingRequest()
    {
        CustomizationRequestScenario scenario;
        Guid versionId;
        await using (var context = _fixture.Database.CreateDbContext())
        {
            scenario = await CustomizationScenarioSeeder.SeedSubmittedRequestAsync(context);
            var source = await context.ProductVersionSet.SingleAsync(
                v => v.ProductVersionId == scenario.Base.ProductVersionId);
            var projectSpecific = new ProductVersion
            {
                ProductVersionId = Guid.NewGuid(),
                ProductId = source.ProductId,
                ProjectId = scenario.Base.ProjectId,
                VersionCode = $"PS-{Guid.NewGuid():N}"[..12],
                VersionName = "Rejected Variant",
                VersionType = ProductVersionType.PROJECT_SPECIFIC,
                DimensionUnit = "cm",
                IsProjectSpecific = true,
                IsPublic = false,
                IsDefault = false,
                Status = ProductStatus.ACTIVE,
                CreatedAt = CoreAccountSeeder.FixedTimestamp
            };
            var reviewingVersion = new CustomizationRequestVersion
            {
                CustomizationRequestVersionId = Guid.NewGuid(),
                CustomizationRequestId = scenario.CustomizationRequestId,
                ProductVersionId = projectSpecific.ProductVersionId,
                VersionNo = 1,
                CreatedByDesignerId = scenario.Base.DesignerAccountId,
                Status = CustomizationVersionStatus.REVIEWING,
                FeasibilityStatus = ProductionFeasibilityStatus.PENDING,
                SubmittedForReviewAt = CoreAccountSeeder.FixedTimestamp,
                CreatedAt = CoreAccountSeeder.FixedTimestamp,
                UpdatedAt = CoreAccountSeeder.FixedTimestamp
            };
            versionId = reviewingVersion.CustomizationRequestVersionId;
            var openRequest = await context.CustomizationRequestSet.FindAsync(scenario.CustomizationRequestId);
            openRequest!.Status = CustomizationStatus.REVIEWING;
            context.ProductVersionSet.Add(projectSpecific);
            context.CustomizationRequestVersionSet.Add(reviewingVersion);
            await context.SaveChangesAsync();
        }

        using var reviewRequest = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Patch,
            $"/api/production/customization-versions/{versionId}/review",
            scenario.Base.ProductionAccountId,
            CoreRoles.Production,
            new ReviewCustomizationVersionDto
            {
                Result = "NOT_FEASIBLE",
                MaterialAvailable = false,
                FeasibilityNote = "Material unavailable"
            });

        var response = await _fixture.Client.SendAsync(reviewRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var verification = _fixture.Database.CreateDbContext();
        var version = await verification.CustomizationRequestVersionSet.SingleAsync();
        Assert.Equal(CustomizationVersionStatus.PRODUCTION_REJECTED, version.Status);
        Assert.Equal(ProductionFeasibilityStatus.NOT_FEASIBLE, version.FeasibilityStatus);
        var request = await verification.CustomizationRequestSet.SingleAsync();
        Assert.Equal(CustomizationStatus.REVIEWING, request.Status);
        Assert.Equal(1, await verification.ProductVersionSet.CountAsync(v => v.IsProjectSpecific == true));
    }

    [Fact]
    public async Task Cancel_AsCustomer_CancelsSubmittedRequest()
    {
        CustomizationRequestScenario scenario;
        await using (var context = _fixture.Database.CreateDbContext())
        {
            scenario = await CustomizationScenarioSeeder.SeedSubmittedRequestAsync(context);
        }

        using var request = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Patch,
            $"/customization-requests/{scenario.CustomizationRequestId}/cancel",
            scenario.Base.CustomerAccountId,
            CoreRoles.Customer,
            new CancelCustomizationRequestDto { CancelReason = "No longer needed" });

        var response = await _fixture.Client.SendAsync(request);
        var result = await response.Content
            .ReadFromJsonAsync<ServiceResult<CustomizationRequestDto>>(IntegrationHttp.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(CustomizationStatus.CANCELLED, result?.Data?.Status);

        await using var verification = _fixture.Database.CreateDbContext();
        var entity = await verification.CustomizationRequestSet.SingleAsync();
        Assert.Equal(CustomizationStatus.CANCELLED, entity.Status);
    }

    [Fact]
    public async Task Accept_WhenAlreadyFeasible_PersistsAcceptedVersionId()
    {
        CustomizationRequestScenario scenario;
        Guid versionId;
        await using (var context = _fixture.Database.CreateDbContext())
        {
            (scenario, versionId) = await CustomizationScenarioSeeder.SeedFeasibleVersionAsync(context);
        }

        using var request = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Post,
            $"/customization-requests/{scenario.CustomizationRequestId}/accept",
            scenario.Base.CustomerAccountId,
            CoreRoles.Customer,
            new AcceptCustomizationRequestDto { CustomizationRequestVersionId = versionId });

        var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var verification = _fixture.Database.CreateDbContext();
        var entity = await verification.CustomizationRequestSet.SingleAsync();
        Assert.Equal(CustomizationStatus.ACCEPTED, entity.Status);
        Assert.Equal(versionId, entity.AcceptedRequestVersionId);
    }
}
