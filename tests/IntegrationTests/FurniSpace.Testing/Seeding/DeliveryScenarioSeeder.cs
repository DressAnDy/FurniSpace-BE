using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;

namespace FurniSpace.Testing.Seeding;

public sealed record DeliveryOrderScenario(
    Guid CustomerAccountId,
    Guid SalesAccountId,
    Guid ProductionAccountId,
    Guid ProjectId,
    Guid OrderId,
    Guid FirstOrderItemId,
    Guid SecondOrderItemId,
    Guid PendingOrderItemId);

public static class DeliveryScenarioSeeder
{
    private const decimal PreVatTotalAmount = 11_111_111.11m;
    private const decimal VatRate = 0.08m;
    private const decimal VatAmount = 888_888.89m;
    private const decimal TotalAmount = 12_000_000m;

    public static async Task<DeliveryOrderScenario> SeedReadyForDeliveryOrderAsync(
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
            $"delivery-customer-{suffix}@integration.test",
            "Delivery Customer");
        var sales = CoreAccountSeeder.CreateAccount(
            roles[CoreRoles.Sales].RoleId,
            $"delivery-sales-{suffix}@integration.test",
            "Delivery Sales");
        var production = CoreAccountSeeder.CreateAccount(
            roles[CoreRoles.Production].RoleId,
            $"delivery-production-{suffix}@integration.test",
            "Delivery Production");

        var project = ProjectScenarioSeeder.CreateProject(
            customer.AccountId,
            sales.AccountId,
            $"PRJ-DEL-{suffix}",
            "Delivery Project",
            ProjectStatus.READY_FOR_DELIVERY);

        var proposal = ProposalScenarioSeeder.CreateProposal(
            project.ProjectId,
            sales.AccountId,
            "Delivery proposal",
            ProposalStatus.SELECTED);

        var quotation = new Quotation
        {
            QuotationId = Guid.NewGuid(),
            ProjectId = project.ProjectId,
            ProposalId = proposal.ProposalId,
            QuotationCode = $"QUO-DEL-{suffix}",
            VersionNo = 1,
            SubtotalAmount = PreVatTotalAmount,
            TotalDiscountAmount = 0m,
            PreVatAmount = PreVatTotalAmount,
            VatRate = VatRate,
            VatAmount = VatAmount,
            TotalAmount = TotalAmount,
            Status = QuotationStatus.ACCEPTED,
            AcceptedAt = CoreAccountSeeder.FixedTimestamp,
            CreatedAt = CoreAccountSeeder.FixedTimestamp
        };

        var order = CreateReadyOrder(project, proposal, quotation, customer.AccountId, sales.AccountId, suffix);
        var (category, product, productVersion) = ProposalScenarioSeeder.CreateCatalog(suffix);
        var firstProductItem = CreateProductOrderItem(order.OrderId, productVersion.ProductVersionId, "Counter", quantity: 2);
        var secondProductItem = CreateProductOrderItem(order.OrderId, productVersion.ProductVersionId, "Shelf", quantity: 1);
        var pendingProductItem = CreatePendingProductOrderItem(order.OrderId, productVersion.ProductVersionId);

        context.AccountSet.AddRange(customer, sales, production);
        context.ProjectSet.Add(project);
        context.ProposalSet.Add(proposal);
        context.QuotationSet.Add(quotation);
        context.OrderSet.Add(order);
        context.CategorySet.Add(category);
        context.ProductSet.Add(product);
        context.ProductVersionSet.Add(productVersion);
        context.OrderItemSet.AddRange(firstProductItem, secondProductItem, pendingProductItem);
        await context.SaveChangesAsync(cancellationToken);

        return new DeliveryOrderScenario(
            customer.AccountId,
            sales.AccountId,
            production.AccountId,
            project.ProjectId,
            order.OrderId,
            firstProductItem.OrderItemId,
            secondProductItem.OrderItemId,
            pendingProductItem.OrderItemId);
    }

    private static Order CreateReadyOrder(
        Project project,
        Proposal proposal,
        Quotation quotation,
        Guid customerId,
        Guid salesId,
        string suffix)
    {
        return new Order
        {
            OrderId = Guid.NewGuid(),
            ProjectId = project.ProjectId,
            ProposalId = proposal.ProposalId,
            QuotationId = quotation.QuotationId,
            OrderCode = $"ORD-DEL-{suffix}",
            CustomerId = customerId,
            SalesId = salesId,
            VatRate = VatRate,
            VatAmount = VatAmount,
            OriginalTotalAmount = TotalAmount,
            FinalTotalAmount = TotalAmount,
            DepositAmount = 3_600_000m,
            PaidAmount = 3_600_000m,
            RemainingAmount = 8_400_000m,
            Status = OrderStatus.READY_FOR_DELIVERY,
            ConfirmedAt = CoreAccountSeeder.FixedTimestamp,
            CreatedAt = CoreAccountSeeder.FixedTimestamp,
            UpdatedAt = CoreAccountSeeder.FixedTimestamp
        };
    }

    private static OrderItem CreateProductOrderItem(
        Guid orderId,
        Guid productVersionId,
        string productName,
        int quantity)
    {
        return new OrderItem
        {
            OrderItemId = Guid.NewGuid(),
            OrderId = orderId,
            ProductVersionId = productVersionId,
            ProductNameSnapshot = productName,
            ProductVersionNameSnapshot = $"{productName} Version",
            ProductVersionCodeSnapshot = $"PV-{productName.ToUpperInvariant()}",
            Quantity = quantity,
            Status = OrderItemStatus.READY,
            UnitPrice = 4_000_000m,
            DiscountAmount = 0m,
            SubtotalAmount = 4_000_000m * quantity
        };
    }

    private static OrderItem CreatePendingProductOrderItem(Guid orderId, Guid productVersionId)
    {
        return new OrderItem
        {
            OrderItemId = Guid.NewGuid(),
            OrderId = orderId,
            ProductVersionId = productVersionId,
            ProductNameSnapshot = "Delivery fee product",
            ProductVersionNameSnapshot = "Delivery fee version",
            ProductVersionCodeSnapshot = "PV-DELIVERY-FEE",
            Quantity = 1,
            Status = OrderItemStatus.PENDING,
            UnitPrice = 500_000m,
            DiscountAmount = 0m,
            SubtotalAmount = 500_000m
        };
    }
}
