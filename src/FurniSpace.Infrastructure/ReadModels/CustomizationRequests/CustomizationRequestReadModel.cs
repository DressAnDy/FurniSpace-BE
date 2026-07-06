using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.CustomizationRequests;

public class CustomizationRequestReadModel
{
    public Guid CustomizationRequestId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid ProposalId { get; set; }
    public Guid ProposalItemId { get; set; }
    public Guid? RequestedByCustomerId { get; set; }
    public string RequestTitle { get; set; } = string.Empty;
    public string? RequestDescription { get; set; }
    public decimal? RequestedWidth { get; set; }
    public decimal? RequestedHeight { get; set; }
    public decimal? RequestedDepth { get; set; }
    public string? RequestedMaterial { get; set; }
    public string? RequestedColor { get; set; }
    public string? RequestedChangeNote { get; set; }
    public Guid? DesignerId { get; set; }
    public string? DesignerSpecNote { get; set; }
    public Guid? ProductionReviewBy { get; set; }
    public string? FeasibilityNote { get; set; }
    public int? EstimatedProductionDays { get; set; }
    public decimal? EstimatedAdditionalCost { get; set; }
    public string? AdditionalCostReason { get; set; }
    public bool? MaterialAvailable { get; set; }
    public string? ProductionRiskNote { get; set; }
    public Guid? SalesReviewBy { get; set; }
    public Guid? ApprovedProductVersionId { get; set; }
    public CustomizationStatus? Status { get; set; }
    public DateTime? CustomerAcceptedAt { get; set; }
    public DateTime? CustomerRejectedAt { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid CustomerId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public Guid? AssignedSalesId { get; set; }
    public Guid? AssignedDesignerId { get; set; }
}
