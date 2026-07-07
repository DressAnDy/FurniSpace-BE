namespace FurniSpace.Application.DTOs.Quotations;

public static class QuotationErrorCodes
{
    public const string ProjectNotFound = "PROJECT_NOT_FOUND";
    public const string QuotationNotFound = "QUOTATION_NOT_FOUND";
    public const string QuotationNotAvailable = "QUOTATION_NOT_AVAILABLE";
    public const string ProjectNotReadyForQuotation = "PROJECT_NOT_READY_FOR_QUOTATION";
    public const string ProposalNotSelected = "PROPOSAL_NOT_SELECTED";
    public const string CustomizationRequestPending = "CUSTOMIZATION_REQUEST_PENDING";
    public const string QuotationAlreadyExists = "QUOTATION_ALREADY_EXISTS";
    public const string InvalidQuotationStatus = "INVALID_QUOTATION_STATUS";
    public const string InvalidQuotationItem = "INVALID_QUOTATION_ITEM";
    public const string QuotationItemNotFound = "QUOTATION_ITEM_NOT_FOUND";
    public const string QuotationItemNotEditable = "QUOTATION_ITEM_NOT_EDITABLE";
    public const string QuotationNotReadyToSend = "QUOTATION_NOT_READY_TO_SEND";
    public const string QuotationExpired = "QUOTATION_EXPIRED";
    public const string InvalidQuotationRevisionReason = "INVALID_QUOTATION_REVISION_REASON";
    public const string InvalidQuotationRejectReason = "INVALID_QUOTATION_REJECT_REASON";
}
