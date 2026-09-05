using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.OperationalDelayReports;

public static class OperationalDelayReportErrorCodes
{
    public const string ProjectNotFound = "OPERATIONAL_DELAY_PROJECT_NOT_FOUND";
    public const string ReportNotFound = "OPERATIONAL_DELAY_REPORT_NOT_FOUND";
    public const string ProductionRequestNotFound = "OPERATIONAL_DELAY_PRODUCTION_REQUEST_NOT_FOUND";
    public const string ProductionRequestProjectMismatch = "OPERATIONAL_DELAY_PRODUCTION_REQUEST_PROJECT_MISMATCH";
    public const string ProductionDeadlineMissing = "OPERATIONAL_DELAY_PRODUCTION_DEADLINE_MISSING";
    public const string TargetCompletionDateMissing = "OPERATIONAL_DELAY_TARGET_COMPLETION_DATE_MISSING";
    public const string OrderProjectMismatch = "OPERATIONAL_DELAY_ORDER_PROJECT_MISMATCH";
    public const string DeliveryProjectMismatch = "OPERATIONAL_DELAY_DELIVERY_PROJECT_MISMATCH";
    public const string Forbidden = "OPERATIONAL_DELAY_FORBIDDEN";
    public const string InvalidRequest = "OPERATIONAL_DELAY_INVALID_REQUEST";
}

public sealed class CreateProductionDelayReportRequestDto
{
    public Guid ProductionRequestId { get; set; }
    public string? ReasonCode { get; set; }
    public string ReasonDetail { get; set; } = string.Empty;
}

public sealed class CreateDeliveryDelayReportRequestDto
{
    public Guid? OrderId { get; set; }
    public Guid? DeliveryId { get; set; }
    public string? ReasonCode { get; set; }
    public string ReasonDetail { get; set; } = string.Empty;
}

public sealed class OperationalDelayReportDto
{
    public Guid OperationalDelayReportId { get; set; }
    public Guid ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public string ReportPhase { get; set; } = string.Empty;
    public Guid? ProductionRequestId { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? DeliveryId { get; set; }
    public DateOnly DeadlineSnapshot { get; set; }
    public string DelayState { get; set; } = string.Empty;
    public string? ReasonCode { get; set; }
    public string ReasonDetail { get; set; } = string.Empty;
    public Guid ReportedBy { get; set; }
    public string? ReporterName { get; set; }
    public DateTime ReportedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class OperationalDelayReportListResponseDto
{
    public IReadOnlyList<OperationalDelayReportDto> Items { get; set; } = [];
}
