using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;

namespace FurniSpace.Testing.Seeding;

public sealed record FinalPaymentOrderScenario(
    Guid CustomerAccountId,
    Guid SalesAccountId,
    Guid ProjectId,
    Guid OrderId,
    Guid ProductOrderItemId,
    decimal FinalTotalAmount,
    decimal PaidAmount,
    decimal RemainingAmount);

public static class FinalPaymentScenarioSeeder
{
    private const decimal PreVatTotalAmount = 11_111_111.11m;
    private const decimal VatRate = 0.08m;
    private const decimal VatAmount = 888_888.89m;
    private const decimal FinalTotalAmount = 12_000_000m;
    private const decimal DepositPaidAmount = 3_600_000m;

    public static Task<FinalPaymentOrderScenario> SeedDeliveredOrderWithRemainingAsync(
        AppDbContext context,
        CancellationToken cancellationToken = default)
    {
        return SeedDeliveredOrderAsync(context, DepositPaidAmount, cancellationToken);
    }

    public static Task<FinalPaymentOrderScenario> SeedDeliveredFullyPaidOrderAsync(
        AppDbContext context,
        CancellationToken cancellationToken = default)
    {
        return SeedDeliveredOrderAsync(context, FinalTotalAmount, cancellationToken);
    }

    private static async Task<FinalPaymentOrderScenario> SeedDeliveredOrderAsync(
        AppDbContext context,
        decimal paidAmount,
        CancellationToken cancellationToken)
    {
        var roles = await CoreAccountSeeder.EnsureRolesAsync(
            context,
            cancellationToken,
            CoreRoles.Customer,
            CoreRoles.Sales);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var customer = CoreAccountSeeder.CreateAccount(
            roles[CoreRoles.Customer].RoleId,
            $"final-payment-customer-{suffix}@integration.test",
            "Final Payment Customer");
        var sales = CoreAccountSeeder.CreateAccount(
            roles[CoreRoles.Sales].RoleId,
            $"final-payment-sales-{suffix}@integration.test",
            "Final Payment Sales");

        var project = ProjectScenarioSeeder.CreateProject(
            customer.AccountId,
            sales.AccountId,
            $"PRJ-FP-{suffix}",
            "Final Payment Project",
            ProjectStatus.DELIVERED);

        var proposal = ProposalScenarioSeeder.CreateProposal(
            project.ProjectId,
            sales.AccountId,
            "Final payment proposal",
            ProposalStatus.SELECTED);

        var quotation = CreateQuotation(project.ProjectId, proposal.ProposalId, suffix);
        var order = CreateDeliveredOrder(project, proposal, quotation, customer.AccountId, sales.AccountId, paidAmount, suffix);
        var (category, product, productVersion) = ProposalScenarioSeeder.CreateCatalog(suffix);
        var orderItem = CreateDeliveredProductOrderItem(
            order.OrderId,
            productVersion.ProductVersionId,
            sales.AccountId);
        var paidPayment = CreatePaidPayment(project.ProjectId, order.OrderId, quotation.QuotationId, customer.AccountId, paidAmount, suffix);

        context.AccountSet.AddRange(customer, sales);
        context.ProjectSet.Add(project);
        context.ProposalSet.Add(proposal);
        context.QuotationSet.Add(quotation);
        context.OrderSet.Add(order);
        context.CategorySet.Add(category);
        context.ProductSet.Add(product);
        context.ProductVersionSet.Add(productVersion);
        context.OrderItemSet.Add(orderItem);
        context.PaymentSet.Add(paidPayment);
        await context.SaveChangesAsync(cancellationToken);

        return new FinalPaymentOrderScenario(
            customer.AccountId,
            sales.AccountId,
            project.ProjectId,
            order.OrderId,
            orderItem.OrderItemId,
            FinalTotalAmount,
            paidAmount,
            FinalTotalAmount - paidAmount);
    }

    private static Quotation CreateQuotation(Guid projectId, Guid proposalId, string suffix)
    {
        return new Quotation
        {
            QuotationId = Guid.NewGuid(),
            ProjectId = projectId,
            ProposalId = proposalId,
            QuotationCode = $"QUO-FP-{suffix}",
            VersionNo = 1,
            SubtotalAmount = PreVatTotalAmount,
            TotalDiscountAmount = 0m,
            PreVatAmount = PreVatTotalAmount,
            VatRate = VatRate,
            VatAmount = VatAmount,
            TotalAmount = FinalTotalAmount,
            Status = QuotationStatus.ACCEPTED,
            AcceptedAt = CoreAccountSeeder.FixedTimestamp,
            CreatedAt = CoreAccountSeeder.FixedTimestamp
        };
    }

    private static Order CreateDeliveredOrder(
        Project project,
        Proposal proposal,
        Quotation quotation,
        Guid customerId,
        Guid salesId,
        decimal paidAmount,
        string suffix)
    {
        return new Order
        {
            OrderId = Guid.NewGuid(),
            ProjectId = project.ProjectId,
            ProposalId = proposal.ProposalId,
            QuotationId = quotation.QuotationId,
            OrderCode = $"ORD-FP-{suffix}",
            CustomerId = customerId,
            SalesId = salesId,
            VatRate = VatRate,
            VatAmount = VatAmount,
            OriginalTotalAmount = FinalTotalAmount,
            FinalTotalAmount = FinalTotalAmount,
            DepositAmount = paidAmount,
            PaidAmount = paidAmount,
            RemainingAmount = FinalTotalAmount - paidAmount,
            Status = OrderStatus.DELIVERED,
            ConfirmedAt = CoreAccountSeeder.FixedTimestamp,
            CustomerConfirmedDeliveryAt = CoreAccountSeeder.FixedTimestamp,
            CreatedAt = CoreAccountSeeder.FixedTimestamp,
            UpdatedAt = CoreAccountSeeder.FixedTimestamp
        };
    }

    private static OrderItem CreateDeliveredProductOrderItem(
        Guid orderId,
        Guid productVersionId,
        Guid deliveredBy)
    {
        return new OrderItem
        {
            OrderItemId = Guid.NewGuid(),
            OrderId = orderId,
            ProductVersionId = productVersionId,
            ProductNameSnapshot = "Delivered counter",
            ProductVersionNameSnapshot = "Delivered counter version",
            ProductVersionCodeSnapshot = "PV-FP-001",
            Quantity = 1,
            Status = OrderItemStatus.DELIVERED,
            DeliveredAt = CoreAccountSeeder.FixedTimestamp,
            DeliveredBy = deliveredBy,
            UnitPrice = PreVatTotalAmount,
            DiscountAmount = 0m,
            SubtotalAmount = PreVatTotalAmount
        };
    }

    private static Payment CreatePaidPayment(
        Guid projectId,
        Guid orderId,
        Guid quotationId,
        Guid customerId,
        decimal amount,
        string suffix)
    {
        return new Payment
        {
            PaymentId = Guid.NewGuid(),
            ProjectId = projectId,
            OrderId = orderId,
            QuotationId = quotationId,
            PaymentCode = $"PAY-FP-{suffix}",
            PaidBy = customerId,
            PaymentType = PaymentType.DEPOSIT,
            Amount = amount,
            Currency = "VND",
            Status = PaymentStatus.PAID,
            PaidAt = CoreAccountSeeder.FixedTimestamp,
            CreatedAt = CoreAccountSeeder.FixedTimestamp,
            UpdatedAt = CoreAccountSeeder.FixedTimestamp
        };
    }
}
