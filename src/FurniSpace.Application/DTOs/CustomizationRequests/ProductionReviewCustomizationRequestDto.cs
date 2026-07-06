namespace FurniSpace.Application.DTOs.CustomizationRequests;

public sealed class ProductionReviewCustomizationRequestDto
{
    public string Result { get; set; } = string.Empty;
    public bool? MaterialAvailable { get; set; }
    public int? EstimatedProductionDays { get; set; }
    public decimal? EstimatedAdditionalCost { get; set; }
    public string? AdditionalCostReason { get; set; }
    public string? FeasibilityNote { get; set; }
    public string? ProductionRiskNote { get; set; }
}
