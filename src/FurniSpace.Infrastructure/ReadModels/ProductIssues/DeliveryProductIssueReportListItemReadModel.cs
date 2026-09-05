using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.ProductIssues;

public class DeliveryProductIssueReportListItemReadModel
{
    public Guid DeliveryProductIssueReportId { get; init; }
    public Guid ProjectId { get; init; }
    public Guid OrderId { get; init; }
    public Guid OrderItemId { get; init; }
    public Guid? DeliveryItemId { get; init; }
    public DeliveryProductIssueType IssueType { get; init; }
    public string Description { get; init; } = string.Empty;
    public int? AffectedQuantity { get; init; }
    public Guid ReportedBy { get; init; }
    public string? ReporterName { get; init; }
    public DateTime ReportedAt { get; init; }
    public DateTime CreatedAt { get; init; }
}
