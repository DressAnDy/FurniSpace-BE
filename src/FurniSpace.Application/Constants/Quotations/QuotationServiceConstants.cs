using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.Constants.Quotations;

internal static class QuotationServiceConstants
{
    internal const string ProjectNotFoundMessage = "Project not found.";
    internal const string QuotationNotFoundMessage = "Quotation not found.";
    internal const string QuotationItemNotFoundMessage = "Quotation item not found.";
    internal const string QuotationItemsNotEditableMessage = "Quotation items cannot be updated in this quotation status.";
    internal const string QuotationCodeParameter = "QuotationCode";
    internal const string QuotationReferenceType = "QUOTATION";

    internal static readonly QuotationStatus[] CustomerVisibleStatuses =
    [
        QuotationStatus.SENT,
        QuotationStatus.REVISION_REQUESTED,
        QuotationStatus.REVISED,
        QuotationStatus.ACCEPTED,
        QuotationStatus.REJECTED,
        QuotationStatus.EXPIRED
    ];

    internal static readonly QuotationStatus[] HeaderEditableStatuses =
    [
        QuotationStatus.DRAFT,
        QuotationStatus.REVISION_REQUESTED,
        QuotationStatus.REVISED
    ];

    internal static readonly QuotationStatus[] ManualItemEditableStatuses =
    [
        QuotationStatus.DRAFT,
        QuotationStatus.REVISION_REQUESTED,
        QuotationStatus.REVISED
    ];
}
