namespace FurniSpace.Application.Common.Payments;

internal static class ProjectStartFeeAccessEvaluator
{
    public static bool CanManage(string? role, Guid? assignedSalesId, Guid currentUserId)
    {
        return role switch
        {
            ProjectAssignmentAccessEvaluator.AdminRole => true,
            ProjectAssignmentAccessEvaluator.SalesRole => assignedSalesId == currentUserId,
            _ => false
        };
    }
}
