#nullable enable

namespace FurniSpace.Application.DTOs.Production;

public sealed class ProductionCompletionDto
{
    public Guid ProductionRequestId { get; set; }
    public string ProductionStatus { get; set; } = string.Empty;
    public string OrderStatus { get; set; } = string.Empty;
    public string ProjectStatus { get; set; } = string.Empty;
    public DateOnly? ActualStartDate { get; set; }
    public DateOnly? ActualCompletionDate { get; set; }
    public int ReadyOrderItemCount { get; set; }
    public int UnavailableOrderItemCount { get; set; }
    public decimal FinalTotalAmount { get; set; }
    public decimal? PaidAmount { get; set; }
    public decimal? RemainingAmount { get; set; }
}
