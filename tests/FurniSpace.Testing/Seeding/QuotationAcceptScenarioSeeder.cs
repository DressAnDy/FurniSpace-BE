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

public static class QuotationAcceptScenarioSeeder
{
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
