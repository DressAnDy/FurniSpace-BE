using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;

namespace FurniSpace.Testing.Seeding;

public sealed record ProjectConsultationScenario(
    Guid ProjectId,
    Guid CustomerAccountId,
    Guid SalesAccountId);

public static class ProjectScenarioSeeder
{
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
}
