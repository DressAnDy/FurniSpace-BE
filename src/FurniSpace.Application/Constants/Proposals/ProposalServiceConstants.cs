using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.Constants.Proposals;

internal static class ProposalServiceConstants
{
    internal const string AdminRole = "ADMIN";
    internal const string CustomerRole = "CUSTOMER";
    internal const string DesignerRole = "DESIGNER";
    internal const string SalesRole = "SALES";

    internal const int MaxPageSize = 100;
    internal const int MaxProposalNameLength = 150;
    internal const int MaxDescriptionLength = 1000;
    internal const int MaxSceneNameLength = 150;
    internal const int MaxCustomizationNoteLength = 1000;

    internal const string AuthenticatedAccountIdRequiredMessage = "Authenticated account id is required.";
    internal const string ProjectIdRequiredMessage = "Project id is required.";
    internal const string ProposalIdRequiredMessage = "Proposal id is required.";
    internal const string ProposalNotFoundMessage = "Proposal not found.";
    internal const string ProposalItemNotFoundMessage = "Proposal item not found.";
    internal const string ProposalSceneNotFoundMessage = "Proposal scene not found.";

    internal const string InvalidProposalStatusCode = "INVALID_PROPOSAL_STATUS";
    internal const string ProposalNotFoundCode = "PROPOSAL_NOT_FOUND";
    internal const string ProposalItemNotFoundCode = "PROPOSAL_ITEM_NOT_FOUND";
    internal const string ProposalNotEditableCode = "PROPOSAL_NOT_EDITABLE";
    internal const string ProposalSceneNotFoundCode = "PROPOSAL_SCENE_NOT_FOUND";
    internal const string RoomPlannerSceneNotFoundCode = "ROOM_PLANNER_SCENE_NOT_FOUND";
    internal const string InvalidProductVersionCode = "INVALID_PRODUCT_VERSION";
    internal const string InvalidQuantityCode = "INVALID_QUANTITY";
    internal const string SceneObjectNotFoundCode = "SCENE_OBJECT_NOT_FOUND";
    internal const string DuplicateSceneObjectProductVersionCode = "DUPLICATE_SCENE_OBJECT_PRODUCT_VERSION";

    internal static readonly ProposalStatus[] CustomerVisibleStatuses =
    [
        ProposalStatus.PUBLISHED,
        ProposalStatus.REVISION_REQUESTED,
        ProposalStatus.SELECTED,
        ProposalStatus.REJECTED
    ];
}
