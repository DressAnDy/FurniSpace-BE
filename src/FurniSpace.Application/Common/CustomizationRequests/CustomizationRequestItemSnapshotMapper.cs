using FurniSpace.Application.DTOs.CustomizationRequests;
using FurniSpace.Domain.Entities;

namespace FurniSpace.Application.Common.CustomizationRequests;

internal static class CustomizationRequestItemSnapshotMapper
{
    public static CustomizationRequestItemSnapshotDto ToDto(ProposalItem item)
    {
        return new CustomizationRequestItemSnapshotDto
        {
            ProposalItemId = item.ProposalItemId,
            ProposalId = item.ProposalId,
            ProductVersionId = item.ProductVersionId,
            ItemName = item.ItemName,
            ItemType = item.ItemType,
            Quantity = item.Quantity,
            Width = item.Width,
            Height = item.Height,
            Depth = item.Depth,
            Material = item.Material,
            Color = item.Color,
            UnitPriceSnapshot = item.UnitPriceSnapshot,
            TotalPriceSnapshot = item.TotalPriceSnapshot,
            Note = item.Note
        };
    }
}
