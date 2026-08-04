using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;

namespace FurniSpace.Testing.Seeding;

public sealed record ProductionOrderScenario(
    Guid CustomerAccountId,
    Guid SalesAccountId,
    Guid ProductionAccountId,
    Guid SecondProductionAccountId,
    Guid InactiveProductionAccountId,
    Guid ProjectId,
    Guid OrderId,
    Guid ProductOrderItemId,
    Guid ManualOrderItemId);

public static class ProductionScenarioSeeder
{
    public static async Task<ProductionOrderScenario> SeedDepositPaidOrderAsync(
        AppDbContext context,
        CancellationToken cancellationToken = default)
    {
        var roles = await CoreAccountSeeder.EnsureRolesAsync(
            context,
            cancellationToken,
            CoreRoles.Customer,
            CoreRoles.Sales,
            CoreRoles.Production);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var customer = CoreAccountSeeder.CreateAccount(
            roles[CoreRoles.Customer].RoleId,
            $"production-customer-{suffix}@integration.test",
            "Production Customer");
        var sales = CoreAccountSeeder.CreateAccount(
            roles[CoreRoles.Sales].RoleId,
            $"production-sales-{suffix}@integration.test",
            "Production Sales");
        var production = CoreAccountSeeder.CreateAccount(
            roles[CoreRoles.Production].RoleId,
            $"production-staff-{suffix}@integration.test",
            "Production Staff");
        var secondProduction = CoreAccountSeeder.CreateAccount(
            roles[CoreRoles.Production].RoleId,
            $"production-staff-second-{suffix}@integration.test",
            "Second Production Staff");
        var inactiveProduction = CoreAccountSeeder.CreateAccount(
            roles[CoreRoles.Production].RoleId,
            $"production-staff-inactive-{suffix}@integration.test",
            "Inactive Production Staff");
        inactiveProduction.Status = AccountStatus.INACTIVE;

        var project = ProjectScenarioSeeder.CreateProject(
            customer.AccountId,
            sales.AccountId,
            $"PRJ-PRD-{suffix}",
            "Production Project",
            ProjectStatus.ORDER_CONFIRMED);

        var proposal = new Proposal
        {
            ProposalId = Guid.NewGuid(),
            ProjectId = project.ProjectId,
            ProposalName = "Selected production proposal",
            Status = ProposalStatus.SELECTED,
            CreatedAt = CoreAccountSeeder.FixedTimestamp
        };

        var quotation = new Quotation
        {
            QuotationId = Guid.NewGuid(),
            ProjectId = project.ProjectId,
            ProposalId = proposal.ProposalId,
            QuotationCode = $"QUO-PRD-{suffix}",
            VersionNo = 1,
            SubtotalAmount = 10_000_000m,
            DiscountAmount = 0m,
            TaxAmount = 0m,
            TotalAmount = 10_000_000m,
            Status = QuotationStatus.ACCEPTED,
            AcceptedAt = CoreAccountSeeder.FixedTimestamp,
            CreatedAt = CoreAccountSeeder.FixedTimestamp
        };

        var order = new Order
        {
            OrderId = Guid.NewGuid(),
            ProjectId = project.ProjectId,
            ProposalId = proposal.ProposalId,
            QuotationId = quotation.QuotationId,
            OrderCode = $"ORD-PRD-{suffix}",
            CustomerId = customer.AccountId,
            SalesId = sales.AccountId,
            OriginalTotalAmount = 10_000_000m,
            ItemAdjustmentAmount = 0m,
            AdditionalDiscountAmount = 0m,
            FinalTotalAmount = 10_000_000m,
            DepositAmount = 3_000_000m,
            PaidAmount = 3_000_000m,
            RemainingAmount = 7_000_000m,
            Status = OrderStatus.DEPOSIT_PAID,
            ConfirmedAt = CoreAccountSeeder.FixedTimestamp,
            CreatedAt = CoreAccountSeeder.FixedTimestamp,
            UpdatedAt = CoreAccountSeeder.FixedTimestamp
        };

        var (category, product, productVersion) = ProposalScenarioSeeder.CreateCatalog(suffix);
        var productItem = CreateOrderItem(
            order.OrderId,
            QuotationItemType.PRODUCT_ITEM,
            productVersion.ProductVersionId,
            product.ProductName);
        var manualItem = CreateOrderItem(
            order.OrderId,
            QuotationItemType.MANUAL_ITEM,
            productVersionId: null,
            "Shipping");

        context.AccountSet.AddRange(customer, sales, production, secondProduction, inactiveProduction);
        context.ProjectSet.Add(project);
        context.ProposalSet.Add(proposal);
        context.QuotationSet.Add(quotation);
        context.OrderSet.Add(order);
        context.CategorySet.Add(category);
        context.ProductSet.Add(product);
        context.ProductVersionSet.Add(productVersion);
        context.OrderItemSet.AddRange(productItem, manualItem);
        context.PaymentSet.Add(new Payment
        {
            PaymentId = Guid.NewGuid(),
            ProjectId = project.ProjectId,
            OrderId = order.OrderId,
            PaymentCode = $"PAY-PRD-{suffix}",
            PaidBy = customer.AccountId,
            PaymentType = PaymentType.DEPOSIT,
            Amount = 3_000_000m,
            Currency = "VND",
            Status = PaymentStatus.PAID,
            PaidAt = CoreAccountSeeder.FixedTimestamp,
            CreatedAt = CoreAccountSeeder.FixedTimestamp,
            UpdatedAt = CoreAccountSeeder.FixedTimestamp
        });
        await context.SaveChangesAsync(cancellationToken);

        return new ProductionOrderScenario(
            customer.AccountId,
            sales.AccountId,
            production.AccountId,
            secondProduction.AccountId,
            inactiveProduction.AccountId,
            project.ProjectId,
            order.OrderId,
            productItem.OrderItemId,
            manualItem.OrderItemId);
    }

    private static OrderItem CreateOrderItem(
        Guid orderId,
        QuotationItemType itemType,
        Guid? productVersionId,
        string productName)
    {
        return new OrderItem
        {
            OrderItemId = Guid.NewGuid(),
            OrderId = orderId,
            ItemType = itemType,
            ProductVersionId = productVersionId,
            ProductNameSnapshot = productName,
            ProductVersionNameSnapshot = $"{productName} Version",
            ProductVersionCodeSnapshot = productVersionId.HasValue ? "PV-PROD-001" : null,
            Quantity = 2,
            DeliveredQuantity = 0,
            Status = OrderItemStatus.PENDING,
            UnitPrice = 5_000_000m,
            SubtotalAmount = 10_000_000m,
            ProductionNote = "Use premium finish"
        };
    }
}
