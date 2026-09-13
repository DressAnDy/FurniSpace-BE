using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Testing.Seeding;

public sealed record CustomizationReadyScenario(
    Guid ProjectId,
    Guid CustomerAccountId,
    Guid SalesAccountId,
    Guid DesignerAccountId,
    Guid ProductionAccountId,
    Guid UnassignedDesignerAccountId,
    Guid ProposalId,
    Guid ProposalItemId,
    Guid ProductVersionId);

public sealed record CustomizationRequestScenario(
    CustomizationReadyScenario Base,
    Guid CustomizationRequestId);

public static class CustomizationScenarioSeeder
{
    public static async Task<CustomizationReadyScenario> SeedPublishedItemAsync(
        AppDbContext context,
        CancellationToken cancellationToken = default)
    {
        var published = await ProposalScenarioSeeder.SeedPublishedProposalAsync(context, cancellationToken: cancellationToken);
        var roles = await CoreAccountSeeder.EnsureRolesAsync(
            context,
            cancellationToken,
            CoreRoles.Production,
            CoreRoles.Designer);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var production = CoreAccountSeeder.CreateAccount(
            roles[CoreRoles.Production].RoleId,
            $"custom-production-{suffix}@integration.test",
            "Customization Production");
        var unassignedDesigner = CoreAccountSeeder.CreateAccount(
            roles[CoreRoles.Designer].RoleId,
            $"custom-unassigned-designer-{suffix}@integration.test",
            "Unassigned Designer");

        context.AccountSet.AddRange(production, unassignedDesigner);
        await context.SaveChangesAsync(cancellationToken);

        return new CustomizationReadyScenario(
            published.ProjectId,
            published.CustomerAccountId,
            published.SalesAccountId,
            published.DesignerAccountId,
            production.AccountId,
            unassignedDesigner.AccountId,
            published.ProposalId,
            published.ProposalItemId,
            published.ProductVersionId);
    }

    public static async Task<CustomizationRequestScenario> SeedSubmittedRequestAsync(
        AppDbContext context,
        CancellationToken cancellationToken = default)
    {
        var ready = await SeedPublishedItemAsync(context, cancellationToken);
        var request = new CustomizationRequest
        {
            CustomizationRequestId = Guid.NewGuid(),
            ProjectId = ready.ProjectId,
            ProposalId = ready.ProposalId,
            SourceProductVersionId = ready.ProductVersionId,
            RequestedByCustomerId = ready.CustomerAccountId,
            RequestTitle = "Change desk material",
            RequestedMaterial = "Walnut",
            RequestedColor = "Dark brown",
            Status = CustomizationStatus.SUBMITTED,
            CreatedAt = CoreAccountSeeder.FixedTimestamp,
            UpdatedAt = CoreAccountSeeder.FixedTimestamp
        };

        context.CustomizationRequestSet.Add(request);
        await context.SaveChangesAsync(cancellationToken);

        return new CustomizationRequestScenario(ready, request.CustomizationRequestId);
    }

    public static async Task<(CustomizationRequestScenario Scenario, Guid VersionId)> SeedFeasibleVersionAsync(
        AppDbContext context,
        CancellationToken cancellationToken = default)
    {
        var scenario = await SeedSubmittedRequestAsync(context, cancellationToken);
        var request = await context.CustomizationRequestSet.FindAsync(
            [scenario.CustomizationRequestId],
            cancellationToken) ?? throw new InvalidOperationException("Request missing.");

        request.Status = CustomizationStatus.REVIEWING;
        request.UpdatedAt = CoreAccountSeeder.FixedTimestamp;

        var source = await context.ProductVersionSet.SingleAsync(
            v => v.ProductVersionId == scenario.Base.ProductVersionId,
            cancellationToken);

        var projectSpecific = new ProductVersion
        {
            ProductVersionId = Guid.NewGuid(),
            ProductId = source.ProductId,
            ProjectId = scenario.Base.ProjectId,
            VersionCode = $"PS-{Guid.NewGuid():N}"[..12],
            VersionName = "Custom Desk V1",
            VersionType = ProductVersionType.PROJECT_SPECIFIC,
            DimensionUnit = "cm",
            Material = "Walnut",
            Color = "Dark brown",
            Width = 140,
            Height = 75,
            Depth = 60,
            EstimatedPrice = 6_500_000m,
            IsDefault = false,
            IsPublic = false,
            IsProjectSpecific = true,
            Status = ProductStatus.ACTIVE,
            CreatedAt = CoreAccountSeeder.FixedTimestamp
        };

        var version = new CustomizationRequestVersion
        {
            CustomizationRequestVersionId = Guid.NewGuid(),
            CustomizationRequestId = scenario.CustomizationRequestId,
            ProductVersionId = projectSpecific.ProductVersionId,
            VersionNo = 1,
            CreatedByDesignerId = scenario.Base.DesignerAccountId,
            VersionTitle = "Custom Desk V1",
            Status = CustomizationVersionStatus.REVIEWING,
            FeasibilityStatus = ProductionFeasibilityStatus.FEASIBLE,
            MaterialAvailable = true,
            EstimatedProductionDays = 5,
            EstimatedAdditionalCost = 1_500_000m,
            AdditionalCostReason = "Custom finish",
            SubmittedForReviewAt = CoreAccountSeeder.FixedTimestamp,
            ProductionReviewedAt = CoreAccountSeeder.FixedTimestamp,
            ProductionReviewedBy = scenario.Base.ProductionAccountId,
            CreatedAt = CoreAccountSeeder.FixedTimestamp,
            UpdatedAt = CoreAccountSeeder.FixedTimestamp
        };

        context.ProductVersionSet.Add(projectSpecific);
        context.CustomizationRequestVersionSet.Add(version);
        await context.SaveChangesAsync(cancellationToken);

        return (scenario, version.CustomizationRequestVersionId);
    }
}
