#nullable enable

namespace FurniSpace.Application.DTOs.Production;

public sealed class ProductionRequestDetailDto
{
    public Guid ProductionRequestId { get; set; }
    public string? ProductionCode { get; set; }
    public Guid ProjectId { get; set; }
    public string? ProjectCode { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public Guid OrderId { get; set; }
    public string? OrderCode { get; set; }
    public Guid? AssignedTo { get; set; }
    public string? AssignedToName { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Priority { get; set; }
    public DateOnly? EstimatedStartDate { get; set; }
    public DateOnly? EstimatedCompletionDate { get; set; }
    public DateOnly? ProductionDeadline { get; set; }
    public DateOnly? ActualStartDate { get; set; }
    public DateOnly? ActualCompletionDate { get; set; }
    public string? CancellationReason { get; set; }
    public string? Note { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<ProductionItemDto> Items { get; set; } = [];
}
