using System;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Domain.Entities;

public class DeliveryProductIssueReport
{
    public Guid DeliveryProductIssueReportId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid OrderId { get; set; }
    public Guid OrderItemId { get; set; }
    public Guid? DeliveryItemId { get; set; }
    public DeliveryProductIssueType IssueType { get; set; }
    public string Description { get; set; } = string.Empty;
    public int? AffectedQuantity { get; set; }
    public Guid ReportedBy { get; set; }
    public DateTime ReportedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
