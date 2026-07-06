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
}
