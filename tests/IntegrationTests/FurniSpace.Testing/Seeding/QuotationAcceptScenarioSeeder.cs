using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;

namespace FurniSpace.Testing.Seeding;

public sealed record QuotationAcceptScenario(
    Guid CustomerAccountId,
    Guid ProjectId,
    Guid ProposalId,
    Guid QuotationId,
    Guid QuotationItemId);

public sealed record QuotationDraftScenario(
    Guid CustomerAccountId,
    Guid SalesAccountId,
    Guid DesignerAccountId,
    Guid ProjectId,
    Guid ProposalId,
    Guid ProposalItemId,
    Guid ProductVersionId);

public static class QuotationAcceptScenarioSeeder
{
    public static async Task<QuotationDraftScenario> SeedSelectedProposalForQuotationAsync(
        AppDbContext context,
        CancellationToken cancellationToken = default)
    {
        var roles = await CoreAccountSeeder.EnsureRolesAsync(
            context,
            cancellationToken,
            CoreRoles.Customer,
            CoreRoles.Sales,
            CoreRoles.Designer);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var customer = CoreAccountSeeder.CreateAccount(
            roles[CoreRoles.Customer].RoleId,
            $"quotation-draft-customer-{suffix}@integration.test",
            "Quotation Draft Customer");
        var sales = CoreAccountSeeder.CreateAccount(
            roles[CoreRoles.Sales].RoleId,
            $"quotation-draft-sales-{suffix}@integration.test",
            "Quotation Draft Sales");
        var designer = CoreAccountSeeder.CreateAccount(
            roles[CoreRoles.Designer].RoleId,
            $"quotation-draft-designer-{suffix}@integration.test",
            "Quotation Draft Designer");

        var project = ProjectScenarioSeeder.CreateProject(
            customer.AccountId,
            sales.AccountId,
            $"PRJ-QD-{suffix}",
            "Quotation Draft Project",
            ProjectStatus.PROPOSAL_SELECTED,
            designer.AccountId);

        var area = MeasurementScenarioSeeder.CreateArea(
            project.ProjectId,
            "Main Floor",
            ProjectAreaType.FLOOR,
            floorNumber: 1,
            status: ProjectAreaStatus.VERIFIED);

        var (category, product, productVersion) = ProposalScenarioSeeder.CreateCatalog(suffix);
        var proposal = ProposalScenarioSeeder.CreateProposal(
            project.ProjectId,
            designer.AccountId,
            "Selected quotation proposal",
            ProposalStatus.SELECTED);
        proposal.SelectedAt = CoreAccountSeeder.FixedTimestamp;

        var scene = ProposalScenarioSeeder.CreateScene(
            proposal.ProposalId,
            designer.AccountId,
            "Selected room planner scene");
        var item = ProposalScenarioSeeder.CreateProposalItem(
            proposal.ProposalId,
            scene.SceneId,
            area.ProjectAreaId,
            productVersion.ProductVersionId,
            "Quotation sofa",
            quantity: 2,
            unitPrice: 5_000_000m);

        context.AccountSet.AddRange(customer, sales, designer);
        context.ProjectSet.Add(project);
        context.ProjectAreaSet.Add(area);
        context.CategorySet.Add(category);
        context.ProductSet.Add(product);
        context.ProductVersionSet.Add(productVersion);
        context.ProposalSet.Add(proposal);
        context.ProposalSceneSet.Add(scene);
        context.ProposalItemSet.Add(item);
        await context.SaveChangesAsync(cancellationToken);

        return new QuotationDraftScenario(
            customer.AccountId,
            sales.AccountId,
            designer.AccountId,
            project.ProjectId,
            proposal.ProposalId,
            item.ProposalItemId,
            productVersion.ProductVersionId);
    }

    public static async Task<QuotationAcceptScenario> SeedSentQuotationAsync(
        AppDbContext context,
        CancellationToken cancellationToken = default)
    {
        var roles = await CoreAccountSeeder.EnsureRolesAsync(
            context,
            cancellationToken,
            CoreRoles.Customer);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var customer = CoreAccountSeeder.CreateAccount(
            roles[CoreRoles.Customer].RoleId,
            $"quotation-customer-{suffix}@integration.test",
            "Quotation Customer");

        var project = ProjectScenarioSeeder.CreateProject(
            customer.AccountId,
            assignedSalesId: null,
            $"PRJ-{suffix}",
            "Quotation Project",
            ProjectStatus.QUOTATION_SENT);

        var proposal = new Proposal
        {
            ProposalId = Guid.NewGuid(),
            ProjectId = project.ProjectId,
            ProposalName = "Selected design",
            Status = ProposalStatus.SELECTED,
            CreatedAt = CoreAccountSeeder.FixedTimestamp
        };

        var quotation = new Quotation
        {
            QuotationId = Guid.NewGuid(),
            ProjectId = project.ProjectId,
            ProposalId = proposal.ProposalId,
            QuotationCode = $"QUO-{suffix}",
            VersionNo = 1,
            SubtotalAmount = 10_000_000m,
            DiscountAmount = 0m,
            TaxAmount = 0m,
            TotalAmount = 10_000_000m,
            Status = QuotationStatus.SENT,
            ValidUntil = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
            SentAt = CoreAccountSeeder.FixedTimestamp,
            CreatedAt = CoreAccountSeeder.FixedTimestamp
        };

        var quotationItem = new QuotationItem
        {
            QuotationItemId = Guid.NewGuid(),
            QuotationId = quotation.QuotationId,
            ItemType = QuotationItemType.MANUAL_ITEM,
            ItemName = "Design service",
            Quantity = 1,
            UnitPrice = 10_000_000m,
            SubtotalAmount = 10_000_000m,
            DiscountAmount = 0m,
            CustomizationAdditionalCost = 0m
        };

        context.AccountSet.Add(customer);
        context.ProjectSet.Add(project);
        context.ProposalSet.Add(proposal);
        context.QuotationSet.Add(quotation);
        context.QuotationItemSet.Add(quotationItem);
        await context.SaveChangesAsync(cancellationToken);

        return new QuotationAcceptScenario(
            customer.AccountId,
            project.ProjectId,
            proposal.ProposalId,
            quotation.QuotationId,
            quotationItem.QuotationItemId);
    }
}
