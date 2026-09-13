using System;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Domain.Entities;

public class CustomizationRequestVersion
{
    public Guid CustomizationRequestVersionId { get; set; }
    public Guid CustomizationRequestId { get; set; }
    public Guid ProductVersionId { get; set; }
    public int VersionNo { get; set; }
    public Guid CreatedByDesignerId { get; set; }
    public string? VersionTitle { get; set; }
    public string? DesignerNote { get; set; }
    public CustomizationVersionStatus Status { get; set; }
    public Guid? ProductionReviewedBy { get; set; }
    public ProductionFeasibilityStatus FeasibilityStatus { get; set; }
    public string? FeasibilityNote { get; set; }
    public int? EstimatedProductionDays { get; set; }
    public decimal? EstimatedAdditionalCost { get; set; }
    public string? AdditionalCostReason { get; set; }
    public bool? MaterialAvailable { get; set; }
    public string? ProductionRiskNote { get; set; }
    public string? AlternativeMaterialNote { get; set; }
    public DateTime? SubmittedForReviewAt { get; set; }
    public DateTime? ProductionReviewedAt { get; set; }
    public DateTime? ProductionRejectedAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? WithdrawnAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public CustomizationRequest? CustomizationRequest { get; set; }
    public ProductVersion? ProductVersion { get; set; }
}
