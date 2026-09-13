using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.Common.Projects;

internal static class ProjectReopenQuotationSupport
{
    internal static bool CanCancelForReopen(QuotationStatus? status)
    {
        return status is QuotationStatus.DRAFT
            or QuotationStatus.SENT
            or QuotationStatus.ACCEPTED;
    }

    internal static void CancelForReopen(Quotation quotation, DateTime utcNow)
    {
        quotation.Status = QuotationStatus.CANCELLED;
        quotation.UpdatedAt = utcNow;
    }
}
