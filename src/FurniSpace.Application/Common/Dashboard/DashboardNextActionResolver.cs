using FurniSpace.Application.Constants.Dashboard;
using FurniSpace.Domain.Enums;
using static FurniSpace.Application.Constants.Dashboard.DashboardQueueConstants;

namespace FurniSpace.Application.Common.Dashboard;

public sealed record DashboardNextActionResult(
    string Group,
    string Phase,
    string Action,
    string ActionPath,
    string Priority,
    string? Warning);

public static class DashboardNextActionResolver
{
    public static DashboardNextActionResult ResolveSales(
        ProjectStatus? projectStatus,
        Guid projectId,
        Guid? orderId,
        OrderStatus? orderStatus,
        decimal? remainingAmount,
        DateTime? customerConfirmedDeliveryAt,
        string? dueBucket)
    {
        if (orderStatus == OrderStatus.DEPOSIT_PENDING && orderId.HasValue)
        {
            return new DashboardNextActionResult(
                GroupOrderAndPayment,
                orderStatus.ToString()!,
                "Follow up deposit",
                DashboardActionPaths.Order(orderId.Value),
                BoostIfOverdue(PriorityHigh, dueBucket),
                "Payment follow-up");
        }

        if (orderStatus == OrderStatus.FINAL_PAYMENT_PENDING && orderId.HasValue)
        {
            var remaining = remainingAmount ?? 0m;
            if (remaining > 0m)
            {
                return new DashboardNextActionResult(
                    GroupOrderAndPayment,
                    orderStatus.ToString()!,
                    "Create Remaining Payment",
                    DashboardActionPaths.Order(orderId.Value),
                    BoostIfOverdue(PriorityHigh, dueBucket),
                    "Remaining unpaid");
            }

            if (!customerConfirmedDeliveryAt.HasValue)
            {
                return new DashboardNextActionResult(
                    GroupOrderAndPayment,
                    orderStatus.ToString()!,
                    "Waiting delivery confirm",
                    DashboardActionPaths.Order(orderId.Value),
                    BoostIfOverdue(PriorityMedium, dueBucket),
                    "Waiting customer confirm");
            }
        }

        return projectStatus switch
        {
            ProjectStatus.SUBMITTED => new DashboardNextActionResult(
                GroupIntake,
                projectStatus.ToString()!,
                "Review request",
                DashboardActionPaths.Project(projectId),
                BoostIfOverdue(PriorityHigh, dueBucket),
                null),

            ProjectStatus.IN_CONSULTATION => new DashboardNextActionResult(
                GroupIntake,
                projectStatus.ToString()!,
                "Continue consultation",
                DashboardActionPaths.Project(projectId),
                BoostIfOverdue(PriorityMedium, dueBucket),
                null),

            ProjectStatus.NEED_BASIC_INFORMATION => new DashboardNextActionResult(
                GroupIntake,
                projectStatus.ToString()!,
                "Waiting customer info",
                DashboardActionPaths.Project(projectId),
                BoostIfOverdue(PriorityMedium, dueBucket),
                "Waiting customer"),

            ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT => new DashboardNextActionResult(
                GroupIntake,
                projectStatus.ToString()!,
                "Assign designer",
                DashboardActionPaths.Project(projectId),
                BoostIfOverdue(PriorityHigh, dueBucket),
                null),

            ProjectStatus.MEASUREMENT_REQUIRED or ProjectStatus.SPACE_VERIFIED => new DashboardNextActionResult(
                GroupDesign,
                projectStatus.ToString()!,
                "Follow design progress",
                DashboardActionPaths.Project(projectId),
                BoostIfOverdue(PriorityLow, dueBucket),
                null),

            ProjectStatus.PROPOSAL_CONSULTING or ProjectStatus.PROPOSAL_SELECTED => new DashboardNextActionResult(
                GroupProposalAndQuotation,
                projectStatus.ToString()!,
                "Manage proposal",
                DashboardActionPaths.ProjectProposals(projectId),
                BoostIfOverdue(PriorityMedium, dueBucket),
                null),

            ProjectStatus.QUOTATION_SENT => new DashboardNextActionResult(
                GroupProposalAndQuotation,
                projectStatus.ToString()!,
                "Waiting quotation accept",
                DashboardActionPaths.ProjectQuotations(projectId),
                BoostIfOverdue(PriorityMedium, dueBucket),
                "Waiting customer"),

            ProjectStatus.QUOTATION_REVISION_REQUESTED => new DashboardNextActionResult(
                GroupProposalAndQuotation,
                projectStatus.ToString()!,
                "Revise quotation",
                DashboardActionPaths.ProjectQuotations(projectId),
                BoostIfOverdue(PriorityHigh, dueBucket),
                null),

            ProjectStatus.ORDER_CONFIRMED or ProjectStatus.IN_PRODUCTION => new DashboardNextActionResult(
                GroupOrderAndPayment,
                projectStatus.ToString()!,
                "Monitor order",
                orderId.HasValue ? DashboardActionPaths.Order(orderId.Value) : DashboardActionPaths.Project(projectId),
                BoostIfOverdue(PriorityLow, dueBucket),
                null),

            ProjectStatus.READY_FOR_DELIVERY or ProjectStatus.DELIVERING or ProjectStatus.DELIVERED =>
                new DashboardNextActionResult(
                    GroupDelivery,
                    projectStatus.ToString()!,
                    "Monitor delivery",
                    orderId.HasValue ? DashboardActionPaths.Order(orderId.Value) : DashboardActionPaths.Project(projectId),
                    BoostIfOverdue(PriorityMedium, dueBucket),
                    null),

            ProjectStatus.COMPLETED => new DashboardNextActionResult(
                GroupDelivery,
                projectStatus.ToString()!,
                "Complete",
                DashboardActionPaths.Project(projectId),
                PriorityLow,
                null),

            ProjectStatus.REJECTED => new DashboardNextActionResult(
                GroupIntake,
                projectStatus.ToString()!,
                "Review rejected request",
                DashboardActionPaths.Project(projectId),
                PriorityLow,
                null),

            _ => new DashboardNextActionResult(
                GroupIntake,
                projectStatus?.ToString() ?? "UNKNOWN",
                "Review project",
                DashboardActionPaths.Project(projectId),
                BoostIfOverdue(PriorityMedium, dueBucket),
                null)
        };
    }

    public static DashboardNextActionResult ResolveDesigner(
        ProjectStatus? projectStatus,
        Guid projectId,
        string? dueBucket)
    {
        return projectStatus switch
        {
            ProjectStatus.MEASUREMENT_REQUIRED => new DashboardNextActionResult(
                GroupDesign,
                projectStatus.ToString()!,
                "Complete measurement",
                DashboardActionPaths.Project(projectId),
                BoostIfOverdue(PriorityHigh, dueBucket),
                null),

            ProjectStatus.SPACE_VERIFIED => new DashboardNextActionResult(
                GroupDesign,
                projectStatus.ToString()!,
                "Start proposal",
                DashboardActionPaths.ProjectProposals(projectId),
                BoostIfOverdue(PriorityHigh, dueBucket),
                null),

            ProjectStatus.PROPOSAL_CONSULTING => new DashboardNextActionResult(
                GroupProposalAndQuotation,
                projectStatus.ToString()!,
                "Manage proposals",
                DashboardActionPaths.ProjectProposals(projectId),
                BoostIfOverdue(PriorityMedium, dueBucket),
                null),

            ProjectStatus.PROPOSAL_SELECTED => new DashboardNextActionResult(
                GroupProposalAndQuotation,
                projectStatus.ToString()!,
                "Proposal selected",
                DashboardActionPaths.ProjectProposals(projectId),
                BoostIfOverdue(PriorityLow, dueBucket),
                null),

            ProjectStatus.QUOTATION_REVISION_REQUESTED => new DashboardNextActionResult(
                GroupProposalAndQuotation,
                projectStatus.ToString()!,
                "Support quotation revision",
                DashboardActionPaths.Project(projectId),
                BoostIfOverdue(PriorityMedium, dueBucket),
                null),

            _ => new DashboardNextActionResult(
                GroupDesign,
                projectStatus?.ToString() ?? "UNKNOWN",
                "Review design work",
                DashboardActionPaths.Project(projectId),
                BoostIfOverdue(PriorityLow, dueBucket),
                null)
        };
    }

    public static DashboardNextActionResult ResolveProduction(
        ProductionRequestStatus status,
        Guid productionRequestId,
        Guid projectId,
        string? dueBucket,
        int blockedItemCount = 0)
    {
        if (blockedItemCount > 0 && status == ProductionRequestStatus.IN_PRODUCTION)
        {
            return new DashboardNextActionResult(
                GroupProduction,
                status.ToString(),
                "Resolve blocked items",
                DashboardActionPaths.ProductionRequest(productionRequestId),
                BoostIfOverdue(PriorityHigh, dueBucket),
                "Blocked or unavailable items");
        }

        return status switch
        {
            ProductionRequestStatus.PENDING_REVIEW => new DashboardNextActionResult(
                GroupProduction,
                status.ToString(),
                "Review production request",
                DashboardActionPaths.ProductionRequest(productionRequestId),
                BoostIfOverdue(PriorityHigh, dueBucket),
                null),

            ProductionRequestStatus.FEASIBLE => new DashboardNextActionResult(
                GroupProduction,
                status.ToString(),
                "Start production",
                DashboardActionPaths.ProductionRequest(productionRequestId),
                BoostIfOverdue(PriorityHigh, dueBucket),
                null),

            ProductionRequestStatus.IN_PRODUCTION => new DashboardNextActionResult(
                GroupProduction,
                status.ToString(),
                "Continue / complete production",
                DashboardActionPaths.ProductionRequest(productionRequestId),
                BoostIfOverdue(PriorityMedium, dueBucket),
                null),

            ProductionRequestStatus.COMPLETED => new DashboardNextActionResult(
                GroupProduction,
                status.ToString(),
                "Completed",
                DashboardActionPaths.ProductionRequest(productionRequestId),
                PriorityLow,
                null),

            ProductionRequestStatus.CANCELLED => new DashboardNextActionResult(
                GroupProduction,
                status.ToString(),
                "Cancelled",
                DashboardActionPaths.ProductionRequest(productionRequestId),
                PriorityLow,
                null),

            _ => new DashboardNextActionResult(
                GroupProduction,
                status.ToString(),
                "Review production",
                DashboardActionPaths.ProductionRequest(productionRequestId),
                BoostIfOverdue(PriorityMedium, dueBucket),
                null)
        };
    }

    private static string BoostIfOverdue(string priority, string? dueBucket)
    {
        if (string.Equals(dueBucket, DueBucketOverdue, StringComparison.Ordinal))
        {
            return PriorityHigh;
        }

        return priority;
    }
}
