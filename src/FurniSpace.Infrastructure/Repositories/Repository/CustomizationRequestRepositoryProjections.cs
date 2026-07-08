using System.Linq.Expressions;
using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.ReadModels.CustomizationRequests;

namespace FurniSpace.Infrastructure.Repositories.Repository;

internal static class CustomizationRequestRepositoryProjections
{
    internal sealed class RequestProjectJoin
    {
        public required CustomizationRequest Request { get; init; }

        public required Project Project { get; init; }
    }

    internal static readonly Expression<Func<RequestProjectJoin, CustomizationRequestReadModel>> RequestProjectReadModel =
        joined => new CustomizationRequestReadModel
        {
            CustomizationRequestId = joined.Request.CustomizationRequestId,
            ProjectId = joined.Request.ProjectId,
            ProposalId = joined.Request.ProposalId,
            ProposalItemId = joined.Request.ProposalItemId,
            RequestedByCustomerId = joined.Request.RequestedByCustomerId,
            RequestTitle = joined.Request.RequestTitle,
            RequestDescription = joined.Request.RequestDescription,
            RequestedWidth = joined.Request.RequestedWidth,
            RequestedHeight = joined.Request.RequestedHeight,
            RequestedDepth = joined.Request.RequestedDepth,
            RequestedMaterial = joined.Request.RequestedMaterial,
            RequestedColor = joined.Request.RequestedColor,
            RequestedChangeNote = joined.Request.RequestedChangeNote,
            DesignerId = joined.Request.DesignerId,
            DesignerSpecNote = joined.Request.DesignerSpecNote,
            ProductionReviewBy = joined.Request.ProductionReviewBy,
            FeasibilityNote = joined.Request.FeasibilityNote,
            EstimatedProductionDays = joined.Request.EstimatedProductionDays,
            EstimatedAdditionalCost = joined.Request.EstimatedAdditionalCost,
            AdditionalCostReason = joined.Request.AdditionalCostReason,
            MaterialAvailable = joined.Request.MaterialAvailable,
            ProductionRiskNote = joined.Request.ProductionRiskNote,
            SalesReviewBy = joined.Request.SalesReviewBy,
            ApprovedProductVersionId = joined.Request.ApprovedProductVersionId,
            Status = joined.Request.Status,
            CustomerAcceptedAt = joined.Request.CustomerAcceptedAt,
            CustomerRejectedAt = joined.Request.CustomerRejectedAt,
            CreatedAt = joined.Request.CreatedAt,
            UpdatedAt = joined.Request.UpdatedAt,
            CustomerId = joined.Project.CustomerId,
            ProjectName = joined.Project.ProjectName,
            AssignedSalesId = joined.Project.AssignedSalesId,
            AssignedDesignerId = joined.Project.AssignedDesignerId
        };

    internal sealed class ProductionQueueJoin
    {
        public required CustomizationRequest Request { get; init; }

        public required Project Project { get; init; }

        public required Proposal Proposal { get; init; }

        public required ProposalItem Item { get; init; }
    }

    internal static readonly Expression<Func<ProductionQueueJoin, ProductionCustomizationRequestQueueReadModel>> ProductionQueueReadModel =
        joined => new ProductionCustomizationRequestQueueReadModel
        {
            CustomizationRequestId = joined.Request.CustomizationRequestId,
            ProjectId = joined.Request.ProjectId,
            ProposalId = joined.Request.ProposalId,
            ProposalItemId = joined.Request.ProposalItemId,
            RequestedByCustomerId = joined.Request.RequestedByCustomerId,
            RequestTitle = joined.Request.RequestTitle,
            RequestDescription = joined.Request.RequestDescription,
            RequestedWidth = joined.Request.RequestedWidth,
            RequestedHeight = joined.Request.RequestedHeight,
            RequestedDepth = joined.Request.RequestedDepth,
            RequestedMaterial = joined.Request.RequestedMaterial,
            RequestedColor = joined.Request.RequestedColor,
            RequestedChangeNote = joined.Request.RequestedChangeNote,
            DesignerId = joined.Request.DesignerId,
            DesignerSpecNote = joined.Request.DesignerSpecNote,
            ProductionReviewBy = joined.Request.ProductionReviewBy,
            FeasibilityNote = joined.Request.FeasibilityNote,
            EstimatedProductionDays = joined.Request.EstimatedProductionDays,
            EstimatedAdditionalCost = joined.Request.EstimatedAdditionalCost,
            AdditionalCostReason = joined.Request.AdditionalCostReason,
            MaterialAvailable = joined.Request.MaterialAvailable,
            ProductionRiskNote = joined.Request.ProductionRiskNote,
            SalesReviewBy = joined.Request.SalesReviewBy,
            ApprovedProductVersionId = joined.Request.ApprovedProductVersionId,
            Status = joined.Request.Status,
            CustomerAcceptedAt = joined.Request.CustomerAcceptedAt,
            CustomerRejectedAt = joined.Request.CustomerRejectedAt,
            CreatedAt = joined.Request.CreatedAt,
            UpdatedAt = joined.Request.UpdatedAt,
            CustomerId = joined.Project.CustomerId,
            ProjectName = joined.Project.ProjectName,
            AssignedSalesId = joined.Project.AssignedSalesId,
            AssignedDesignerId = joined.Project.AssignedDesignerId,
            ProposalName = joined.Proposal.ProposalName,
            ProposalStatus = joined.Proposal.Status,
            ProposalItem = joined.Item
        };

    internal static CustomizationRequestDetailReadModel ToDetailReadModel(
        CustomizationRequestReadModel source,
        ProposalItem proposalItem)
    {
        return new CustomizationRequestDetailReadModel
        {
            CustomizationRequestId = source.CustomizationRequestId,
            ProjectId = source.ProjectId,
            ProposalId = source.ProposalId,
            ProposalItemId = source.ProposalItemId,
            RequestedByCustomerId = source.RequestedByCustomerId,
            RequestTitle = source.RequestTitle,
            RequestDescription = source.RequestDescription,
            RequestedWidth = source.RequestedWidth,
            RequestedHeight = source.RequestedHeight,
            RequestedDepth = source.RequestedDepth,
            RequestedMaterial = source.RequestedMaterial,
            RequestedColor = source.RequestedColor,
            RequestedChangeNote = source.RequestedChangeNote,
            DesignerId = source.DesignerId,
            DesignerSpecNote = source.DesignerSpecNote,
            ProductionReviewBy = source.ProductionReviewBy,
            FeasibilityNote = source.FeasibilityNote,
            EstimatedProductionDays = source.EstimatedProductionDays,
            EstimatedAdditionalCost = source.EstimatedAdditionalCost,
            AdditionalCostReason = source.AdditionalCostReason,
            MaterialAvailable = source.MaterialAvailable,
            ProductionRiskNote = source.ProductionRiskNote,
            SalesReviewBy = source.SalesReviewBy,
            ApprovedProductVersionId = source.ApprovedProductVersionId,
            Status = source.Status,
            CustomerAcceptedAt = source.CustomerAcceptedAt,
            CustomerRejectedAt = source.CustomerRejectedAt,
            CreatedAt = source.CreatedAt,
            UpdatedAt = source.UpdatedAt,
            CustomerId = source.CustomerId,
            ProjectName = source.ProjectName,
            AssignedSalesId = source.AssignedSalesId,
            AssignedDesignerId = source.AssignedDesignerId,
            ProposalItem = proposalItem
        };
    }
}
