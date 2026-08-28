#nullable enable

namespace FurniSpace.Infrastructure.ReadModels.Production;

public sealed class ProductionUnavailableItemReadModel
{
    public Guid ProductionItemId { get; set; }
    public Guid ProductionRequestId { get; set; }
    public string? ProductionCode { get; set; }
    public Guid ProjectId { get; set; }
    public string? ProjectCode { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public Guid OrderId { get; set; }
    public string? OrderCode { get; set; }
    public Guid? AssignedTo { get; set; }
    public string? AssignedToName { get; set; }
    public Guid OrderItemId { get; set; }
    public string? ProductNameSnapshot { get; set; }
    public string? ProductVersionNameSnapshot { get; set; }
    public int? Quantity { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? CancellationReason { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public sealed class ProductionUnavailableItemsQueryReadModel
{
    public string? Keyword { get; set; }
    public Guid? AssignedTo { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
