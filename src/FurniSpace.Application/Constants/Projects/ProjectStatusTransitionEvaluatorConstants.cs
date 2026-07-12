using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.Constants.Projects;

internal static class ProjectStatusTransitionEvaluatorConstants
{
    internal const string AdminRole = "ADMIN";
    internal const string CustomerRole = "CUSTOMER";
    internal const string DesignerRole = "DESIGNER";
    internal const string SalesRole = "SALES";
    internal const int MaxNoteLength = 1000;

    internal static readonly HashSet<ProjectStatus> DesignerAllowedTargetStatuses =
    [
        ProjectStatus.MEASUREMENT_REQUIRED,
        ProjectStatus.SPACE_VERIFIED,
        ProjectStatus.PROPOSAL_CONSULTING
    ];

    internal static readonly HashSet<ProjectStatus> DesignerForbiddenTargetStatuses =
    [
        ProjectStatus.PROPOSAL_SELECTED,
        ProjectStatus.QUOTATION_SENT,
        ProjectStatus.QUOTATION_REVISION_REQUESTED,
        ProjectStatus.ORDER_CONFIRMED,
        ProjectStatus.IN_PRODUCTION,
        ProjectStatus.PRODUCTION_BLOCKED,
        ProjectStatus.READY_FOR_DELIVERY,
        ProjectStatus.DELIVERING,
        ProjectStatus.DELIVERED,
        ProjectStatus.COMPLETED,
        ProjectStatus.REJECTED
    ];
}
