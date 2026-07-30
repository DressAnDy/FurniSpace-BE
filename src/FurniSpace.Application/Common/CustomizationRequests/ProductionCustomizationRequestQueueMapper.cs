using FurniSpace.Application.DTOs.CustomizationRequests;
using FurniSpace.Infrastructure.ReadModels.CustomizationRequests;
using Mapster;

namespace FurniSpace.Application.Common.CustomizationRequests;

internal static class ProductionCustomizationRequestQueueMapper
{
    public static ProductionCustomizationRequestQueueItemDto ToDto(
        ProductionCustomizationRequestQueueReadModel item)
    {
        var request = item.Request;
        var dto = request.Adapt<ProductionCustomizationRequestQueueItemDto>();
        dto.Project = new ProductionCustomizationProjectSummaryDto
        {
            ProjectId = request.ProjectId,
            ProjectName = request.ProjectName,
            CustomerId = request.CustomerId,
            AssignedSalesId = request.AssignedSalesId,
            AssignedDesignerId = request.AssignedDesignerId
        };
        dto.Proposal = new ProductionCustomizationProposalSummaryDto
        {
            ProposalId = request.ProposalId,
            ProposalName = item.ProposalName,
            Status = item.ProposalStatus
        };
        dto.SourceProductVersion = CustomizationAcceptedProductVersionFactory.ToSummaryDto(item.SourceProductVersion);
        return dto;
    }
}
