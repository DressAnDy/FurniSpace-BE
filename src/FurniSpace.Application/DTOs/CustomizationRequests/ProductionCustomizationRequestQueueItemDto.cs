using System;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.CustomizationRequests;

public sealed class ProductionCustomizationRequestQueueItemDto
{
    public Guid CustomizationRequestId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid ProposalId { get; set; }
    public Guid ProposalItemId { get; set; }
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
    public CustomizationStatus? Status { get; set; }
    public DateTime? CustomerAcceptedAt { get; set; }
    public DateTime? CustomerRejectedAt { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public ProductionCustomizationProjectSummaryDto Project { get; set; } = new();
    public ProductionCustomizationProposalSummaryDto Proposal { get; set; } = new();
    public ProductionCustomizationProposalItemSummaryDto ProposalItem { get; set; } = new();
}

public sealed class ProductionCustomizationProjectSummaryDto
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public Guid? AssignedSalesId { get; set; }
    public Guid? AssignedDesignerId { get; set; }
}

public sealed class ProductionCustomizationProposalSummaryDto
{
    public Guid ProposalId { get; set; }
    public string ProposalName { get; set; } = string.Empty;
    public ProposalStatus? Status { get; set; }
}

public sealed class ProductionCustomizationProposalItemSummaryDto
{
    public Guid ProposalItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? ItemType { get; set; }
    public int? Quantity { get; set; }
    public decimal? Width { get; set; }
    public decimal? Height { get; set; }
    public decimal? Depth { get; set; }
    public string? Material { get; set; }
    public string? Color { get; set; }
    public decimal? UnitPriceSnapshot { get; set; }
    public decimal? TotalPriceSnapshot { get; set; }
}
