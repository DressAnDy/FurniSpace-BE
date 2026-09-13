using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;

namespace FurniSpace.Testing.Seeding;

public sealed record ProjectConsultationScenario(
    Guid ProjectId,
    Guid CustomerAccountId,
    Guid SalesAccountId);

public sealed record ProjectSubmittedScenario(
    Guid ProjectId,
    Guid CustomerAccountId,
    Guid SalesAccountId);

public sealed record ProjectDesignerReadyScenario(
    Guid ProjectId,
    Guid CustomerAccountId,
    Guid SalesAccountId,
    Guid DesignerAccountId,
    Guid? ProjectStartFeePaymentId);

public static class ProjectScenarioSeeder
{
    public static async Task<ProjectSubmittedScenario> SeedSubmittedAsync(
        AppDbContext context,
        CancellationToken cancellationToken = default)
    {
        var roles = await CoreAccountSeeder.EnsureRolesAsync(
            context,
            cancellationToken,
            CoreRoles.Customer,
            CoreRoles.Sales);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var customer = CoreAccountSeeder.CreateAccount(
            roles[CoreRoles.Customer].RoleId,
            $"submitted-customer-{suffix}@integration.test",
            "Submitted Customer");
        var sales = CoreAccountSeeder.CreateAccount(
            roles[CoreRoles.Sales].RoleId,
            $"submitted-sales-{suffix}@integration.test",
            "Submitted Sales");

        var project = CreateProject(
            customer.AccountId,
            assignedSalesId: null,
            $"PRJ-{suffix}",
            "Submitted Project",
            ProjectStatus.SUBMITTED);

        context.AccountSet.AddRange(customer, sales);
        context.ProjectSet.Add(project);
        await context.SaveChangesAsync(cancellationToken);

        return new ProjectSubmittedScenario(
            project.ProjectId,
            customer.AccountId,
            sales.AccountId);
    }

    public static async Task<ProjectConsultationScenario> SeedInConsultationAsync(
        AppDbContext context,
        CancellationToken cancellationToken = default)
    {
        var roles = await CoreAccountSeeder.EnsureRolesAsync(
            context,
            cancellationToken,
            CoreRoles.Customer,
            CoreRoles.Sales);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var customer = CoreAccountSeeder.CreateAccount(
            roles[CoreRoles.Customer].RoleId,
            $"project-customer-{suffix}@integration.test",
            "Project Customer");
        var sales = CoreAccountSeeder.CreateAccount(
            roles[CoreRoles.Sales].RoleId,
            $"project-sales-{suffix}@integration.test",
            "Project Sales");

        var project = CreateProject(
            customer.AccountId,
            sales.AccountId,
            $"PRJ-{suffix}",
            "Consultation Project",
            ProjectStatus.IN_CONSULTATION);

        context.AccountSet.AddRange(customer, sales);
        context.ProjectSet.Add(project);
        await context.SaveChangesAsync(cancellationToken);

        return new ProjectConsultationScenario(
            project.ProjectId,
            customer.AccountId,
            sales.AccountId);
    }

    public static async Task<ProjectConsultationScenario> SeedNeedBasicInformationAsync(
        AppDbContext context,
        CancellationToken cancellationToken = default)
    {
        var scenario = await SeedInConsultationAsync(context, cancellationToken);
        var project = await context.ProjectSet.FindAsync([scenario.ProjectId], cancellationToken)
            ?? throw new InvalidOperationException("Seeded project was not found.");
        project.Status = ProjectStatus.NEED_BASIC_INFORMATION;
        project.UpdatedAt = CoreAccountSeeder.FixedTimestamp;
        await context.SaveChangesAsync(cancellationToken);
        return scenario;
    }

    public static async Task<ProjectDesignerReadyScenario> SeedWaitingForDesignerAsync(
        AppDbContext context,
        bool includePaidStartFee = true,
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
            $"designer-ready-customer-{suffix}@integration.test",
            "Designer Ready Customer");
        var sales = CoreAccountSeeder.CreateAccount(
            roles[CoreRoles.Sales].RoleId,
            $"designer-ready-sales-{suffix}@integration.test",
            "Designer Ready Sales");
        var designer = CoreAccountSeeder.CreateAccount(
            roles[CoreRoles.Designer].RoleId,
            $"designer-ready-designer-{suffix}@integration.test",
            "Designer Ready Designer");

        var project = CreateProject(
            customer.AccountId,
            sales.AccountId,
            $"PRJ-{suffix}",
            "Designer Ready Project",
            ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT);

        context.AccountSet.AddRange(customer, sales, designer);
        context.ProjectSet.Add(project);

        Guid? paymentId = null;
        if (includePaidStartFee)
        {
            var payment = CreatePaidProjectStartFee(project.ProjectId, customer.AccountId, suffix);
            paymentId = payment.PaymentId;
            context.PaymentSet.Add(payment);
        }

        await context.SaveChangesAsync(cancellationToken);

        return new ProjectDesignerReadyScenario(
            project.ProjectId,
            customer.AccountId,
            sales.AccountId,
            designer.AccountId,
            paymentId);
    }

    public static Project CreateProject(
        Guid customerId,
        Guid? assignedSalesId,
        string projectCode,
        string projectName,
        ProjectStatus status,
        Guid? assignedDesignerId = null) =>
        new()
        {
            ProjectId = Guid.NewGuid(),
            CustomerId = customerId,
            AssignedSalesId = assignedSalesId,
            AssignedDesignerId = assignedDesignerId,
            ProjectCode = projectCode,
            ProjectName = projectName,
            BusinessType = "Office",
            FurnitureRequirement = "Desks and chairs",
            Status = status,
            SubmittedAt = CoreAccountSeeder.FixedTimestamp,
            SalesAssignedAt = assignedSalesId.HasValue ? CoreAccountSeeder.FixedTimestamp : null,
            CreatedAt = CoreAccountSeeder.FixedTimestamp
        };

    public static Payment CreatePaidProjectStartFee(
        Guid projectId,
        Guid customerId,
        string suffix,
        decimal amount = 2_000_000m) =>
        new()
        {
            PaymentId = Guid.NewGuid(),
            ProjectId = projectId,
            PaymentCode = $"PAY-START-{suffix}",
            PaidBy = customerId,
            PaymentType = PaymentType.PROJECT_START_FEE,
            Amount = amount,
            Currency = "VND",
            Status = PaymentStatus.PAID,
            PaidAt = CoreAccountSeeder.FixedTimestamp,
            CreatedAt = CoreAccountSeeder.FixedTimestamp,
            UpdatedAt = CoreAccountSeeder.FixedTimestamp
        };
}
