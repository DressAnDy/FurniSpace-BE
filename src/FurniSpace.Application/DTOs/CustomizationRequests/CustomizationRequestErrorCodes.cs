namespace FurniSpace.Application.DTOs.CustomizationRequests;

public static class CustomizationRequestErrorCodes
{
    public const string ProjectNotFound = "PROJECT_NOT_FOUND";
    public const string CustomizationRequestNotFound = "CUSTOMIZATION_REQUEST_NOT_FOUND";
    public const string ProposalItemNotFound = "PROPOSAL_ITEM_NOT_FOUND";
    public const string ProposalAlreadySelected = "PROPOSAL_ALREADY_SELECTED";
    public const string QuotationAlreadyCreated = "QUOTATION_ALREADY_CREATED";
    public const string InvalidCustomizationRequest = "INVALID_CUSTOMIZATION_REQUEST";
    public const string InvalidCustomizationTransition = "INVALID_CUSTOMIZATION_TRANSITION";
    public const string CustomizationCostRequired = "CUSTOMIZATION_COST_REQUIRED";
    public const string AdditionalCostReasonRequired = "ADDITIONAL_COST_REASON_REQUIRED";
    public const string MaterialNotAvailable = "MATERIAL_NOT_AVAILABLE";
    public const string CustomizationNotReadyForFinalApproval = "CUSTOMIZATION_NOT_READY_FOR_FINAL_APPROVAL";
    public const string CustomizationCostNotApproved = "CUSTOMIZATION_COST_NOT_APPROVED";
    public const string CustomizationNotFeasible = "CUSTOMIZATION_NOT_FEASIBLE";
    public const string InvalidCustomizationDecision = "INVALID_CUSTOMIZATION_DECISION";
    public const string CustomizationAlreadyAccepted = "CUSTOMIZATION_ALREADY_ACCEPTED";
    public const string CustomizationRequestPending = "CUSTOMIZATION_REQUEST_PENDING";
}
