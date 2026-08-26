#nullable enable

using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Reports;

public sealed class AdminProjectReportsQueryDto
{
    public string? Keyword { get; set; }
    public string? Stage { get; set; }
    public ProjectStatus? ProjectStatus { get; set; }
    public string? AttentionReason { get; set; }
    public string? Severity { get; set; }
    public string? OwnerRole { get; set; }
    public Guid? SalesId { get; set; }
    public Guid? DesignerId { get; set; }
    public bool AttentionOnly { get; set; } = true;
    public int? MinAgeDays { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SortBy { get; set; } = "severityDesc";
    public string? SortDirection { get; set; } = "desc";
}

public sealed class AdminProjectReportListItemDto
{
    public Guid ProjectId { get; set; }
    public string? ProjectCode { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public ProjectStatus? ProjectStatus { get; set; }
    public string? Stage { get; set; }
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public Guid? AssignedSalesId { get; set; }
    public string? AssignedSalesName { get; set; }
    public Guid? AssignedDesignerId { get; set; }
    public string? AssignedDesignerName { get; set; }
    public int AgeDays { get; set; }
    public int AgeInStatusDays { get; set; }
    public string AttentionReason { get; set; } = string.Empty;
    public string SuggestedAction { get; set; } = string.Empty;
    public string OwnerRole { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public DateTime? SubmittedAt { get; set; }
}

public sealed class AdminProjectReportDetailDto
{
    public AdminProjectReportHeaderDto Header { get; set; } = new();
    public AdminProjectReportStageHealthDto? CurrentStageHealth { get; set; }
    public AdminProjectReportFlowProgressDto FlowProgress { get; set; } = new();
    public AdminProjectReportCommercialSnapshotDto CommercialSnapshot { get; set; } = new();
    public AdminProjectReportTerminalSummaryDto? TerminalSummary { get; set; }
}

public sealed class AdminProjectReportHeaderDto
{
    public Guid ProjectId { get; set; }
    public string? ProjectCode { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public ProjectStatus? ProjectStatus { get; set; }
    public string? Stage { get; set; }
    public bool IsRejected { get; set; }
    public string? RejectionReason { get; set; }
    public string? BusinessType { get; set; }
    public string? ProjectAddress { get; set; }
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public Guid? AssignedSalesId { get; set; }
    public string? AssignedSalesName { get; set; }
    public Guid? AssignedDesignerId { get; set; }
    public string? AssignedDesignerName { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? SalesAssignedAt { get; set; }
    public DateTime? DesignerAssignedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public int AgeDays { get; set; }
    public int AgeInStatusDays { get; set; }
    public AdminProjectReportAttentionDto? PrimaryAttention { get; set; }
    public IReadOnlyList<string> AllAttentionReasons { get; set; } = [];
}

public sealed class AdminProjectReportAttentionDto
{
    public string Reason { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string OwnerRole { get; set; } = string.Empty;
    public string SuggestedAction { get; set; } = string.Empty;
}

public sealed class AdminProjectReportStageHealthDto
{
    public string Stage { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public ProjectStatus? StatusInStage { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public int AgeInStageDays { get; set; }
    public IReadOnlyList<AdminProjectReportBlockerDto> Blockers { get; set; } = [];
    public AdminProjectReportNextActionDto NextAction { get; set; } = new();
    public IReadOnlyList<AdminProjectReportLinkDto> Links { get; set; } = [];
}

public sealed class AdminProjectReportBlockerDto
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class AdminProjectReportNextActionDto
{
    public string OwnerRole { get; set; } = string.Empty;
    public string SuggestedAction { get; set; } = string.Empty;
}

public sealed class AdminProjectReportLinkDto
{
    public string Type { get; set; } = string.Empty;
    public Guid Id { get; set; }
    public string Label { get; set; } = string.Empty;
}

public sealed class AdminProjectReportFlowProgressDto
{
    public IReadOnlyList<AdminProjectReportFlowStageDto> Stages { get; set; } = [];
}

public sealed class AdminProjectReportFlowStageDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public DateTime? CompletedAt { get; set; }
}

public sealed class AdminProjectReportCommercialSnapshotDto
{
    public decimal? ProjectStartFeeAmount { get; set; }
    public PaymentStatus? ProjectStartFeeStatus { get; set; }
    public DateTime? ProjectStartFeePaidAt { get; set; }
    public Guid? OrderId { get; set; }
    public string? OrderCode { get; set; }
    public OrderStatus? OrderStatus { get; set; }
    public decimal? OrderFinalTotal { get; set; }
    public decimal? OrderPaidAmount { get; set; }
    public decimal? OrderRemainingAmount { get; set; }
    public Guid? ActivePaymentId { get; set; }
    public PaymentType? ActivePaymentType { get; set; }
    public decimal? ActivePaymentAmount { get; set; }
    public PaymentStatus? ActivePaymentStatus { get; set; }
    public decimal TotalProjectCashCollected { get; set; }
    public DateTime? LastPaidAt { get; set; }
}

public sealed class AdminProjectReportTerminalSummaryDto
{
    public string Outcome { get; set; } = string.Empty;
    public DateTime? CompletedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public int? DurationDays { get; set; }
    public string? RejectionReason { get; set; }
    public string? Note { get; set; }
}

public static class AdminProjectReportErrorCodes
{
    public const string FilterInvalid = "PROJECT_REPORT_FILTER_INVALID";
    public const string ProjectNotFound = "PROJECT_NOT_FOUND";
}
