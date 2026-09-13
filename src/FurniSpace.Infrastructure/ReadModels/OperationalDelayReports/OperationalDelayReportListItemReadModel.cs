using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.OperationalDelayReports;

public class OperationalDelayReportListItemReadModel
{
    public Guid OperationalDelayReportId { get; init; }
    public Guid ProjectId { get; init; }
    public OperationalDelayPhase ReportPhase { get; init; }
    public Guid? ProductionRequestId { get; init; }
    public Guid? OrderId { get; init; }
    public Guid? DeliveryId { get; init; }
    public DateOnly DeadlineSnapshot { get; init; }
    public OperationalDelayState DelayState { get; init; }
    public ProductionDelayReasonCode? ProductionReasonCode { get; init; }
    public DeliveryDelayReasonCode? DeliveryReasonCode { get; init; }
    public string ReasonDetail { get; init; } = string.Empty;
    public Guid ReportedBy { get; init; }
    public string? ReporterName { get; init; }
    public DateTime ReportedAt { get; init; }
    public DateTime CreatedAt { get; init; }
}
