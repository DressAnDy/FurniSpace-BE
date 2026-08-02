using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Storage;

namespace FurniSpace.Application.Constants.CustomizationRequests;

internal static class CustomizationRequestServiceConstants
{
    internal const string AllStatusesFilter = "ALL";
    internal const int MaxProductionQueuePageSize = 100;
    internal const int MaxProductVersionPreviewFileCount = ProductPreviewImageSettings.DefaultMaxCount;
    internal const string CustomizationReferenceType = "CUSTOMIZATION_REQUEST";
    internal const string CustomizationVersionReferenceType = "CUSTOMIZATION_REQUEST_VERSION";
    internal const string CustomizationRequestNotFoundMessage = "Customization request not found.";
    internal const string CustomizationVersionNotFoundMessage = "Customization request version not found.";
    internal const string FeasibleResult = "FEASIBLE";
    internal const string NotFeasibleResult = "NOT_FEASIBLE";

    internal static readonly CustomizationStatus[] ActiveRequestStatuses =
    [
        CustomizationStatus.SUBMITTED,
        CustomizationStatus.REVIEWING
    ];

    internal static readonly CustomizationVersionStatus[] NonTerminalVersionStatuses =
    [
        CustomizationVersionStatus.DRAFT,
        CustomizationVersionStatus.REVIEWING
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
