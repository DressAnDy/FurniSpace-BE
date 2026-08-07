using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;

namespace FurniSpace.Testing.Seeding;

public sealed record ProjectChatScenario(
    Guid ProjectId,
    Guid CustomerAccountId,
    Guid SalesAccountId,
    Guid DesignerAccountId,
    Guid SalesChatId,
    Guid DesignerChatId,
    Guid ArchivedChatId,
    Guid LatestMessageId);

public static class ProjectChatScenarioSeeder
{
    public static async Task<ProjectChatScenario> SeedAsync(
        AppDbContext context,
        CancellationToken cancellationToken = default)
    {
        var roles = await CoreAccountSeeder.EnsureRolesAsync(
            context,
            cancellationToken,
            CoreRoles.Sales,
            CoreRoles.Designer,
            CoreRoles.Customer);

        var suffix = Guid.NewGuid().ToString("N");
        var now = CoreAccountSeeder.FixedTimestamp;

        var customer = CoreAccountSeeder.CreateAccount(
            roles[CoreRoles.Customer].RoleId,
            $"customer-{suffix}@example.test",
            "Customer User",
            now);
        var sales = CoreAccountSeeder.CreateAccount(
            roles[CoreRoles.Sales].RoleId,
            $"sales-{suffix}@example.test",
            "Sales User",
            now);
        var designer = CoreAccountSeeder.CreateAccount(
            roles[CoreRoles.Designer].RoleId,
            $"designer-{suffix}@example.test",
            "Designer User",
            now);

        var project = new Project
        {
            ProjectId = Guid.NewGuid(),
            CustomerId = customer.AccountId,
            AssignedSalesId = sales.AccountId,
            AssignedDesignerId = designer.AccountId,
            ProjectCode = $"PRJ-{suffix[..12]}",
            ProjectName = "PostgreSQL Integration Project",
            BusinessType = "Office",
            FurnitureRequirement = "Desks and chairs",
            Status = ProjectStatus.IN_CONSULTATION,
            SubmittedAt = now,
            CreatedAt = now
        };

        var salesChat = CreateChat(
            project.ProjectId,
            sales.AccountId,
            ProjectChatType.SALES,
            ProjectChatStatus.OPEN,
            "Sales chat",
            now.AddMinutes(-10));
        var designerChat = CreateChat(
            project.ProjectId,
            designer.AccountId,
            ProjectChatType.DESIGNER,
            ProjectChatStatus.OPEN,
            "Designer chat",
            now.AddMinutes(-5));
        var archivedChat = CreateChat(
            project.ProjectId,
            sales.AccountId,
            ProjectChatType.GENERAL,
            ProjectChatStatus.ARCHIVED,
            "Archived chat",
            now.AddMinutes(-20));

        var latestMessage = new ProjectChatMessage
        {
            MessageId = Guid.NewGuid(),
            ChatId = salesChat.ChatId,
            SenderId = sales.AccountId,
            MessageType = ProjectChatMessageType.TEXT,
            Content = new string('A', 250),
            CreatedAt = now
        };

        context.AccountSet.AddRange(customer, sales, designer);
        context.ProjectSet.Add(project);
        context.ProjectChatSet.AddRange(salesChat, designerChat, archivedChat);
        context.ProjectChatMessageSet.Add(latestMessage);
        await context.SaveChangesAsync(cancellationToken);

        return new ProjectChatScenario(
            project.ProjectId,
            customer.AccountId,
            sales.AccountId,
            designer.AccountId,
            salesChat.ChatId,
            designerChat.ChatId,
            archivedChat.ChatId,
            latestMessage.MessageId);
    }

    private static ProjectChat CreateChat(
        Guid projectId,
        Guid staffId,
        ProjectChatType chatType,
        ProjectChatStatus status,
        string title,
        DateTime createdAt) =>
        new()
        {
            ChatId = Guid.NewGuid(),
            ProjectId = projectId,
            StaffId = staffId,
            ChatType = chatType,
            Status = status,
            Title = title,
            CreatedAt = createdAt
        };
}
