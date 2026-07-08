using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.CustomizationRequests;

public sealed class ProductionCustomizationRequestQueueReadModel : CustomizationRequestReadModel
{
    public string ProposalName { get; set; } = string.Empty;
    public ProposalStatus? ProposalStatus { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? ItemType { get; set; }
    public int? Quantity { get; set; }
    public decimal? ItemWidth { get; set; }
    public decimal? ItemHeight { get; set; }
    public decimal? ItemDepth { get; set; }
    public string? ItemMaterial { get; set; }
    public string? ItemColor { get; set; }
    public decimal? UnitPriceSnapshot { get; set; }
    public decimal? TotalPriceSnapshot { get; set; }
}
