#nullable enable

using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.Production;

public class ProductionRequestReadModelBase
{
    public Guid ProductionRequestId { get; set; }
    public string? ProductionCode { get; set; }
    public Guid ProjectId { get; set; }
    public string? ProjectCode { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public Guid? AssignedSalesId { get; set; }
    public Guid OrderId { get; set; }
    public string? OrderCode { get; set; }
    public Guid? AssignedTo { get; set; }
    public string? AssignedToName { get; set; }
    public ProductionRequestStatus? Status { get; set; }
    public string? Priority { get; set; }
    public DateOnly? ProductionDeadline { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class ProductionRequestListItemReadModel : ProductionRequestReadModelBase
{
    public int ProductionItemCount { get; set; }
}

public sealed class ProductionRequestDetailReadModel : ProductionRequestListItemReadModel
{
    public DateOnly? ActualStartDate { get; set; }
    public DateOnly? ActualCompletionDate { get; set; }
    public string? CancellationReason { get; set; }
    public string? Note { get; set; }
    public List<ProductionItemReadModel> Items { get; set; } = [];
}

public sealed class ProductionItemReadModel
{
    public Guid ProductionItemId { get; set; }
    public Guid ProductionRequestId { get; set; }
    public Guid OrderItemId { get; set; }
    public Guid? ProductVersionId { get; set; }
    public string? ProductNameSnapshot { get; set; }
    public string? ProductVersionNameSnapshot { get; set; }
    public int? Quantity { get; set; }
    public ProductionItemStatus? Status { get; set; }
    public string? MaterialNote { get; set; }
    public string? ProductionNote { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public OrderItemStatus? OrderItemStatus { get; set; }
}
