using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.Constants.CustomizationRequests;

internal static class CustomizationRequestServiceConstants
{
    internal const string AllStatusesFilter = "ALL";
    internal const int MaxProductionQueuePageSize = 100;
    internal const string CustomizationReferenceType = "CUSTOMIZATION_REQUEST";
    internal const string FeasibleResult = "FEASIBLE";
    internal const string NotFeasibleResult = "NOT_FEASIBLE";
    internal const string AcceptDecision = "ACCEPT";
    internal const string RejectDecision = "REJECT";

    internal static readonly CustomizationStatus[] ProductionVisibleStatuses =
    [
        CustomizationStatus.PRODUCTION_REVIEWING,
        CustomizationStatus.WAITING_FOR_CUSTOMER_FINAL_APPROVAL,
        CustomizationStatus.NOT_FEASIBLE,
        CustomizationStatus.ACCEPTED
    ];

    internal static readonly ProjectStatus[] ProjectStatusesAfterProposalSelection =
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
        ProjectStatus.COMPLETED
    ];
}
