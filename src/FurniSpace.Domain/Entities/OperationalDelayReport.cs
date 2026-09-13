using System;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Domain.Entities;

public class OperationalDelayReport
{
    public Guid OperationalDelayReportId { get; set; }
    public Guid ProjectId { get; set; }
    public OperationalDelayPhase ReportPhase { get; set; }
    public Guid? ProductionRequestId { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? DeliveryId { get; set; }
    public DateOnly DeadlineSnapshot { get; set; }
    public OperationalDelayState DelayState { get; set; }
    public ProductionDelayReasonCode? ProductionReasonCode { get; set; }
    public DeliveryDelayReasonCode? DeliveryReasonCode { get; set; }
    public string ReasonDetail { get; set; } = string.Empty;
    public Guid ReportedBy { get; set; }
    public DateTime ReportedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
