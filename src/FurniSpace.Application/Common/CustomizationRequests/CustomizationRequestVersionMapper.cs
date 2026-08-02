using FurniSpace.Application.DTOs.CustomizationRequests;
using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.ReadModels.CustomizationRequests;

namespace FurniSpace.Application.Common.CustomizationRequests;

internal static class CustomizationRequestVersionMapper
{
    public static CustomizationRequestVersionDto ToDto(
        CustomizationRequestVersion version,
        ProductVersion productVersion)
    {
        return new CustomizationRequestVersionDto
        {
            CustomizationRequestVersionId = version.CustomizationRequestVersionId,
            CustomizationRequestId = version.CustomizationRequestId,
            VersionNo = version.VersionNo,
            CreatedByDesignerId = version.CreatedByDesignerId,
            VersionTitle = version.VersionTitle,
            DesignerNote = version.DesignerNote,
            Status = version.Status,
            FeasibilityStatus = version.FeasibilityStatus,
            FeasibilityNote = version.FeasibilityNote,
            EstimatedProductionDays = version.EstimatedProductionDays,
            EstimatedAdditionalCost = version.EstimatedAdditionalCost,
            AdditionalCostReason = version.AdditionalCostReason,
            MaterialAvailable = version.MaterialAvailable,
            ProductionRiskNote = version.ProductionRiskNote,
            AlternativeMaterialNote = version.AlternativeMaterialNote,
            SubmittedForReviewAt = version.SubmittedForReviewAt,
            ProductionReviewedAt = version.ProductionReviewedAt,
            ProductionRejectedAt = version.ProductionRejectedAt,
            AcceptedAt = version.AcceptedAt,
            WithdrawnAt = version.WithdrawnAt,
            CreatedAt = version.CreatedAt,
            UpdatedAt = version.UpdatedAt,
            ProductVersion = CustomizationAcceptedProductVersionFactory.ToProductVersionDto(productVersion)
        };
    }

    public static CustomizationRequestVersionDto ToDto(CustomizationRequestVersionReadModel version)
    {
        return new CustomizationRequestVersionDto
        {
            CustomizationRequestVersionId = version.CustomizationRequestVersionId,
            CustomizationRequestId = version.CustomizationRequestId,
            VersionNo = version.VersionNo,
            CreatedByDesignerId = version.CreatedByDesignerId,
            VersionTitle = version.VersionTitle,
            DesignerNote = version.DesignerNote,
            Status = version.Status,
            FeasibilityStatus = version.FeasibilityStatus,
            FeasibilityNote = version.FeasibilityNote,
            EstimatedProductionDays = version.EstimatedProductionDays,
            EstimatedAdditionalCost = version.EstimatedAdditionalCost,
            AdditionalCostReason = version.AdditionalCostReason,
            MaterialAvailable = version.MaterialAvailable,
            ProductionRiskNote = version.ProductionRiskNote,
            AlternativeMaterialNote = version.AlternativeMaterialNote,
            SubmittedForReviewAt = version.SubmittedForReviewAt,
            ProductionReviewedAt = version.ProductionReviewedAt,
            ProductionRejectedAt = version.ProductionRejectedAt,
            AcceptedAt = version.AcceptedAt,
            WithdrawnAt = version.WithdrawnAt,
            CreatedAt = version.CreatedAt,
            UpdatedAt = version.UpdatedAt,
            ProductVersion = CustomizationAcceptedProductVersionFactory.ToProductVersionDto(version.ProductVersion)
        };
    }
}
