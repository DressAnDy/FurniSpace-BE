#nullable enable

namespace FurniSpace.Application.DTOs.Production;

public sealed class ProductionCompletionDto
{
    public Guid ProductionRequestId { get; set; }
    public string ProductionStatus { get; set; } = string.Empty;
    public string OrderStatus { get; set; } = string.Empty;
    public string ProjectStatus { get; set; } = string.Empty;
    public int AppliedAdjustmentCount { get; set; }
    public decimal FinalTotalAmount { get; set; }
    public decimal? PaidAmount { get; set; }
    public decimal? RemainingAmount { get; set; }
}
