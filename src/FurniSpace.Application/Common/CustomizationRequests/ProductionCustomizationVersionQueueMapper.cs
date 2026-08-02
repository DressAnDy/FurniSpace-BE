using FurniSpace.Application.DTOs.CustomizationRequests;
using FurniSpace.Infrastructure.ReadModels.CustomizationRequests;
using Mapster;

namespace FurniSpace.Application.Common.CustomizationRequests;

internal static class ProductionCustomizationVersionQueueMapper
{
    public static ProductionCustomizationVersionQueueItemDto ToDto(
        ProductionCustomizationVersionQueueReadModel item)
    {
        var request = item.Request;
        return new ProductionCustomizationVersionQueueItemDto
        {
            Version = CustomizationRequestVersionMapper.ToDto(item.Version),
            Request = request.Adapt<CustomizationRequestDto>(),
            Project = new ProductionCustomizationProjectSummaryDto
            {
                ProjectId = request.ProjectId,
                ProjectName = request.ProjectName,
                CustomerId = request.CustomerId,
                AssignedSalesId = request.AssignedSalesId,
                AssignedDesignerId = request.AssignedDesignerId
            },
            Proposal = new ProductionCustomizationProposalSummaryDto
            {
                ProposalId = request.ProposalId,
                ProposalName = item.ProposalName,
                Status = item.ProposalStatus
            },
            SourceProductVersion = CustomizationAcceptedProductVersionFactory.ToSummaryDto(item.SourceProductVersion)
        };
    }

    public static ProductionCustomizationVersionDetailDto ToDetailDto(
        ProductionCustomizationVersionDetailReadModel item,
        CustomizationRequestDto requestDetail)
    {
        var request = item.Request;
        return new ProductionCustomizationVersionDetailDto
        {
            Version = CustomizationRequestVersionMapper.ToDto(item.Version),
            Request = requestDetail,
            Project = new ProductionCustomizationProjectSummaryDto
            {
                ProjectId = request.ProjectId,
                ProjectName = request.ProjectName,
                CustomerId = request.CustomerId,
                AssignedSalesId = request.AssignedSalesId,
                AssignedDesignerId = request.AssignedDesignerId
            },
            Proposal = new ProductionCustomizationProposalSummaryDto
            {
                ProposalId = request.ProposalId,
                ProposalName = item.ProposalName,
                Status = item.ProposalStatus
            },
            SourceProductVersion = CustomizationAcceptedProductVersionFactory.ToSummaryDto(item.SourceProductVersion)
        };
    }
}
