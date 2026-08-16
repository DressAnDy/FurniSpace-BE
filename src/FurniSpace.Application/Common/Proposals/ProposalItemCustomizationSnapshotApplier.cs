using FurniSpace.Domain.Entities;

namespace FurniSpace.Application.Common.Proposals;

internal static class ProposalItemCustomizationSnapshotApplier
{
    internal static void ApplyAcceptedProductVersion(
        ProposalItem proposalItem,
        ProductVersion acceptedProductVersion,
        DateTime now)
    {
        var quantity = proposalItem.Quantity ?? 1;
        var unitPrice = acceptedProductVersion.EstimatedPrice ?? 0m;

        proposalItem.ProductVersionId = acceptedProductVersion.ProductVersionId;
        proposalItem.Width = acceptedProductVersion.Width;
        proposalItem.Height = acceptedProductVersion.Height;
        proposalItem.Depth = acceptedProductVersion.Depth;
        proposalItem.Material = acceptedProductVersion.Material;
        proposalItem.Color = acceptedProductVersion.Color;
        proposalItem.IsCustomized = true;
        proposalItem.UnitPriceSnapshot = unitPrice;
        proposalItem.TotalPriceSnapshot = unitPrice * quantity;
        proposalItem.UpdatedAt = now;
    }
}
