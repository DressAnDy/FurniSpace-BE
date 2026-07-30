using System.Linq.Expressions;
using System.Reflection;
using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.ReadModels.CustomizationRequests;

namespace FurniSpace.Infrastructure.Repositories.Repository;

internal static class CustomizationRequestRepositoryProjections
{
    private static readonly PropertyInfo[] ReadModelCopyProperties =
        typeof(CustomizationRequestReadModel).GetProperties(BindingFlags.Instance | BindingFlags.Public);

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
            ProductVersionId = joined.Request.ProductVersionId,
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

    internal static CustomizationRequestDetailReadModel ToDetailReadModel(
        CustomizationRequestReadModel source)
    {
        var detail = new CustomizationRequestDetailReadModel();
        CopyReadModelValues(source, detail);
        return detail;
    }

    private static void CopyReadModelValues(
        CustomizationRequestReadModel source,
        CustomizationRequestReadModel destination)
    {
        foreach (var property in ReadModelCopyProperties)
        {
            if (!property.CanRead || !property.CanWrite)
            {
                continue;
            }

            property.SetValue(destination, property.GetValue(source));
        }
    }
}
