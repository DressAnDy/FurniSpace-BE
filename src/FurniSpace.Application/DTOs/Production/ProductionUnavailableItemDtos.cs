#nullable enable

namespace FurniSpace.Application.DTOs.Production;

public sealed class ProductionUnavailableItemsQueryDto
{
    public string? Keyword { get; set; }
    public Guid? AssignedTo { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed class ProductionUnavailableItemsResponseDto
{
    public List<ProductionUnavailableItemDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
}

public sealed class ProductionUnavailableItemDto
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
