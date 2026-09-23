using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.Common.Proposals;

public static class ProposalItemCustomizationMarker
{
    public static bool HasCustomizationNote(ProposalItem item) =>
        !string.IsNullOrWhiteSpace(item.Note);

    public static bool HasCustomizationNote(string? note) =>
        !string.IsNullOrWhiteSpace(note);

    public static bool IsAcceptedCustomizationProductVersion(
        Guid? productVersionId,
        ProductVersionType? versionType,
        IReadOnlySet<Guid> acceptedCustomizationProductVersionIds)
    {
        if (productVersionId is null || productVersionId == Guid.Empty)
        {
            return false;
        }

        if (versionType != ProductVersionType.PROJECT_SPECIFIC)
        {
            return false;
        }

        return acceptedCustomizationProductVersionIds.Contains(productVersionId.Value);
    }

    public static bool ResolveIsCustomized(
        ProposalItem item,
        ProductVersionType? versionType,
        IReadOnlySet<Guid> acceptedCustomizationProductVersionIds)
    {
        if (HasCustomizationNote(item))
        {
            return true;
        }

        if (IsAcceptedCustomizationProductVersion(
                item.ProductVersionId,
                versionType,
                acceptedCustomizationProductVersionIds))
        {
            return true;
        }

        return item.IsCustomized ?? false;
    }

    /// <summary>
    /// Quotation draft: accepted product version ids are already PROJECT_SPECIFIC customization versions.
    /// </summary>
    public static bool ResolveIsCustomizedForQuotation(
        ProposalItem item,
        IReadOnlySet<Guid> acceptedCustomizationProductVersionIds)
    {
        if (HasCustomizationNote(item))
        {
            return true;
        }

        if (item.ProductVersionId is { } productVersionId
            && acceptedCustomizationProductVersionIds.Contains(productVersionId))
        {
            return true;
        }

        return item.IsCustomized ?? false;
    }
}
