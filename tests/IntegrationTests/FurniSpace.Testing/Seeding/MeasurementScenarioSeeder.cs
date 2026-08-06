using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;

namespace FurniSpace.Testing.Seeding;

public sealed record MeasurementScenario(
    Guid ProjectId,
    Guid CustomerAccountId,
    Guid SalesAccountId,
    Guid DesignerAccountId,
    Guid? OtherCustomerAccountId = null);

public static class MeasurementScenarioSeeder
{
    public static async Task<MeasurementScenario> SeedMeasurementRequiredAsync(
        AppDbContext context,
        bool includeOtherCustomer = false,
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
            $"measurement-customer-{suffix}@integration.test",
            "Measurement Customer");
        var sales = CoreAccountSeeder.CreateAccount(
            roles[CoreRoles.Sales].RoleId,
            $"measurement-sales-{suffix}@integration.test",
            "Measurement Sales");
        var designer = CoreAccountSeeder.CreateAccount(
            roles[CoreRoles.Designer].RoleId,
            $"measurement-designer-{suffix}@integration.test",
            "Measurement Designer");

        context.AccountSet.AddRange(customer, sales, designer);

        Guid? otherCustomerId = null;
        if (includeOtherCustomer)
        {
            var other = CoreAccountSeeder.CreateAccount(
                roles[CoreRoles.Customer].RoleId,
                $"measurement-other-{suffix}@integration.test",
                "Other Customer");
            context.AccountSet.Add(other);
            otherCustomerId = other.AccountId;
        }

        var project = ProjectScenarioSeeder.CreateProject(
            customer.AccountId,
            sales.AccountId,
            $"PRJ-M-{suffix}",
            "Measurement Project",
            ProjectStatus.MEASUREMENT_REQUIRED,
            designer.AccountId);
        context.ProjectSet.Add(project);
        await context.SaveChangesAsync(cancellationToken);

        return new MeasurementScenario(
            project.ProjectId,
            customer.AccountId,
            sales.AccountId,
            designer.AccountId,
            otherCustomerId);
    }

    public static ProjectArea CreateArea(
        Guid projectId,
        string areaName,
        ProjectAreaType areaType,
        Guid? parentAreaId = null,
        ProjectAreaStatus status = ProjectAreaStatus.DRAFT,
        int? floorNumber = null) =>
        new()
        {
            ProjectAreaId = Guid.NewGuid(),
            ProjectId = projectId,
            ParentAreaId = parentAreaId,
            AreaName = areaName,
            AreaType = areaType,
            FloorNumber = floorNumber,
            Status = status,
            CreatedAt = CoreAccountSeeder.FixedTimestamp
        };
}
