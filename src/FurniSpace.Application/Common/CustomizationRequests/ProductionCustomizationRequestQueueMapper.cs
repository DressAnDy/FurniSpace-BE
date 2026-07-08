using FurniSpace.Application.DTOs.CustomizationRequests;
using FurniSpace.Infrastructure.ReadModels.CustomizationRequests;
using Mapster;

namespace FurniSpace.Application.Common.CustomizationRequests;

internal static class ProductionCustomizationRequestQueueMapper
{
    public static ProductionCustomizationRequestQueueItemDto ToDto(
        ProductionCustomizationRequestQueueReadModel item)
    {
        var dto = item.Adapt<ProductionCustomizationRequestQueueItemDto>();
        dto.Project = new ProductionCustomizationProjectSummaryDto
        {
            ProjectId = item.ProjectId,
            ProjectName = item.ProjectName,
            CustomerId = item.CustomerId,
            AssignedSalesId = item.AssignedSalesId,
            AssignedDesignerId = item.AssignedDesignerId
        };
        dto.Proposal = new ProductionCustomizationProposalSummaryDto
        {
            ProposalId = item.ProposalId,
            ProposalName = item.ProposalName,
            Status = item.ProposalStatus
        };
        dto.ProposalItem = new ProductionCustomizationProposalItemSummaryDto
        {
            ProposalItemId = item.ProposalItemId,
            ItemName = item.ItemName,
            ItemType = item.ItemType,
            Quantity = item.Quantity,
            Width = item.ItemWidth,
            Height = item.ItemHeight,
            Depth = item.ItemDepth,
            Material = item.ItemMaterial,
            Color = item.ItemColor,
            UnitPriceSnapshot = item.UnitPriceSnapshot,
            TotalPriceSnapshot = item.TotalPriceSnapshot
        };
        return dto;
    }
}
