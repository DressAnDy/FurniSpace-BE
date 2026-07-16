using FurniSpace.Infrastructure.ReadModels.Proposals;

namespace FurniSpace.Application.Common;

internal static class ProjectAssignmentAccessEvaluator
{
    public const string AdminRole = "ADMIN";
    public const string CustomerRole = "CUSTOMER";
    public const string SalesRole = "SALES";
    public const string DesignerRole = "DESIGNER";

    public static bool CanAccessProjectAssignment(
        string? role,
        Guid customerId,
        Guid? assignedSalesId,
        Guid? assignedDesignerId,
        Guid currentUserId)
    {
        return role switch
        {
            AdminRole => true,
            CustomerRole => customerId == currentUserId,
            SalesRole => assignedSalesId == currentUserId,
            DesignerRole => assignedDesignerId == currentUserId,
            _ => false
        };
    }

    public static bool CanAccessProjectAssignment(
        string? role,
        ProposalProjectAccessReadModel project,
        Guid currentUserId)
    {
        return CanAccessProjectAssignment(
            role,
            project.CustomerId,
            project.AssignedSalesId,
            project.AssignedDesignerId,
            currentUserId);
    }

    public static bool CanManageAsAssignedSales(
        string? role,
        Guid? assignedSalesId,
        Guid currentUserId)
    {
        return role == AdminRole || role == SalesRole && assignedSalesId == currentUserId;
    }
}
