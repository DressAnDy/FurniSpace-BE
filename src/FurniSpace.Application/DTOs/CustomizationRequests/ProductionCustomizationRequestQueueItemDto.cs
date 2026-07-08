using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.CustomizationRequests;

public sealed class ProductionCustomizationRequestQueueItemDto : CustomizationRequestDto
{
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
