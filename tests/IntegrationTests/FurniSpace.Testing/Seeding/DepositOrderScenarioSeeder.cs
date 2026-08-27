using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;

namespace FurniSpace.Testing.Seeding;

public sealed record DepositOrderScenario(
    Guid CustomerAccountId,
    Guid ProjectId,
    Guid OrderId,
    Guid QuotationId,
    decimal DepositAmount);

public static class DepositOrderScenarioSeeder
{
    public static async Task<DepositOrderScenario> SeedDepositPendingOrderAsync(
        AppDbContext context,
        decimal totalAmount = 10_000_000m,
        decimal depositAmount = 3_000_000m,
        CancellationToken cancellationToken = default)
    {
        var roles = await CoreAccountSeeder.EnsureRolesAsync(
            context,
            cancellationToken,
            CoreRoles.Customer);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var customer = CoreAccountSeeder.CreateAccount(
            roles[CoreRoles.Customer].RoleId,
            $"deposit-customer-{suffix}@integration.test",
            "Deposit Customer");

        var project = ProjectScenarioSeeder.CreateProject(
            customer.AccountId,
            assignedSalesId: null,
            $"PRJ-{suffix}",
            "Deposit Project",
            ProjectStatus.ORDER_CONFIRMED);

        var proposal = new Proposal
        {
            ProposalId = Guid.NewGuid(),
            ProjectId = project.ProjectId,
            ProposalName = "Selected design",
            Status = ProposalStatus.SELECTED,
            CreatedAt = CoreAccountSeeder.FixedTimestamp
        };

        decimal preVatAmount = totalAmount;
        decimal vatAmount = Math.Round(preVatAmount * 0.08m, 2, MidpointRounding.AwayFromZero);
        decimal vatInclusiveTotal = preVatAmount + vatAmount;

        var quotation = new Quotation
        {
            QuotationId = Guid.NewGuid(),
            ProjectId = project.ProjectId,
            ProposalId = proposal.ProposalId,
            QuotationCode = $"QUO-{suffix}",
            VersionNo = 1,
            SubtotalAmount = preVatAmount,
            TotalDiscountAmount = 0m,
            PreVatAmount = preVatAmount,
            VatRate = 0.08m,
            VatAmount = vatAmount,
            TotalAmount = vatInclusiveTotal,
            Status = QuotationStatus.ACCEPTED,
            ValidUntil = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
            AcceptedAt = CoreAccountSeeder.FixedTimestamp,
            CreatedAt = CoreAccountSeeder.FixedTimestamp
        };

        var order = new Order
        {
            OrderId = Guid.NewGuid(),
            ProjectId = project.ProjectId,
            ProposalId = proposal.ProposalId,
            QuotationId = quotation.QuotationId,
            OrderCode = $"ORD-{suffix}",
            CustomerId = customer.AccountId,
            VatRate = 0.08m,
            VatAmount = vatAmount,
            OriginalTotalAmount = vatInclusiveTotal,
            ItemAdjustmentAmount = 0m,
            AdditionalDiscountAmount = 0m,
            FinalTotalAmount = vatInclusiveTotal,
            DepositAmount = depositAmount,
            PaidAmount = 0m,
            RemainingAmount = vatInclusiveTotal,
            DeliveryAddress = "123 Nguyen Trai, District 1",
            ReceiverName = "Deposit Receiver",
            ReceiverPhone = "0901234567",
            DeliveryNote = "Call before delivery.",
            Status = OrderStatus.DEPOSIT_PENDING,
            ConfirmedAt = CoreAccountSeeder.FixedTimestamp,
            CreatedAt = CoreAccountSeeder.FixedTimestamp,
            UpdatedAt = CoreAccountSeeder.FixedTimestamp
        };

        context.AccountSet.Add(customer);
        context.ProjectSet.Add(project);
        context.ProposalSet.Add(proposal);
        context.QuotationSet.Add(quotation);
        context.OrderSet.Add(order);
        context.ProjectPhaseTimelineSet.Add(new ProjectPhaseTimeline
        {
            ProjectPhaseTimelineId = Guid.NewGuid(),
            ProjectId = project.ProjectId,
            Phase = ProjectPhaseType.PRODUCTION,
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            CreatedBy = customer.AccountId,
            UpdatedBy = customer.AccountId,
            CreatedAt = CoreAccountSeeder.FixedTimestamp,
            UpdatedAt = CoreAccountSeeder.FixedTimestamp
        });
        await context.SaveChangesAsync(cancellationToken);

        return new DepositOrderScenario(
            customer.AccountId,
            project.ProjectId,
            order.OrderId,
            quotation.QuotationId,
            depositAmount);
    }
}
