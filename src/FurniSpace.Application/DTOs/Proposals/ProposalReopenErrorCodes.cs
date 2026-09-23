namespace FurniSpace.Application.DTOs.Proposals;

public static class ProposalReopenErrorCodes
{
    public const string ReopenNotAllowed = "PROPOSAL_REOPEN_NOT_ALLOWED";
    public const string ProposalHasQuotation = "PROPOSAL_HAS_QUOTATION";
    public const string ProposalAlreadySelected = "PROPOSAL_ALREADY_SELECTED";
    public const string QuotationHasOrder = "PROPOSAL_QUOTATION_HAS_ORDER";
    public const string QuotationCannotBeCancelled = "PROPOSAL_QUOTATION_CANNOT_BE_CANCELLED";
}
