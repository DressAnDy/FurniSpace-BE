using FurniSpace.Domain.Entities;

namespace FurniSpace.Application.Common.Quotations;

internal static class QuotationInactiveProposalLinkSupport
{
    internal static void DetachProposalItemLinks(
        IReadOnlyList<QuotationItem> quotationItems,
        DateTime utcNow)
    {
        foreach (var quotationItem in quotationItems)
        {
            if (quotationItem.ProposalItemId is null)
            {
                continue;
            }

            quotationItem.ProposalItemId = null;
            quotationItem.UpdatedAt = utcNow;
        }
    }
}
