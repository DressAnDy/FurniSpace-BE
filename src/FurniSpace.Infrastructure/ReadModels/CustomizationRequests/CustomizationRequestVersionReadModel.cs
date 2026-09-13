using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.CustomizationRequests;

public sealed class CustomizationRequestVersionReadModel
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
    public ProductVersion ProductVersion { get; set; } = new();
}

public sealed class ProductionCustomizationVersionQueueReadModel
{
    public CustomizationRequestVersionReadModel Version { get; set; } = new();
    public CustomizationRequestReadModel Request { get; set; } = new();
    public string ProposalName { get; set; } = string.Empty;
    public ProposalStatus? ProposalStatus { get; set; }
    public ProductVersion SourceProductVersion { get; set; } = new();
}

public sealed class ProductionCustomizationVersionQueueQueryReadModel
{
    public IReadOnlyList<CustomizationVersionStatus>? Statuses { get; set; }
    public IReadOnlyList<ProductionFeasibilityStatus>? FeasibilityStatuses { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? ProposalId { get; set; }
    public bool? MaterialAvailable { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public sealed class ProductionCustomizationVersionDetailReadModel
{
    public CustomizationRequestVersionReadModel Version { get; set; } = new();
    public CustomizationRequestReadModel Request { get; set; } = new();
    public string ProposalName { get; set; } = string.Empty;
    public ProposalStatus? ProposalStatus { get; set; }
    public ProductVersion SourceProductVersion { get; set; } = new();
}
