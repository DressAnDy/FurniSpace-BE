using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.CustomizationRequests;

public sealed record ProductionVersionReviewUpdate(
    Guid CustomizationRequestVersionId,
    ProductionFeasibilityStatus FeasibilityStatus,
    CustomizationVersionStatus VersionStatus,
    Guid ProductionReviewedBy,
    string? FeasibilityNote,
    int? EstimatedProductionDays,
    decimal? EstimatedAdditionalCost,
    string? AdditionalCostReason,
    bool? MaterialAvailable,
    string? ProductionRiskNote,
    string? AlternativeMaterialNote,
    DateTime ReviewedAt);
