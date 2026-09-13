#nullable enable

using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.Reports;

public sealed class AdminProjectReportListQueryReadModel
{
    public string? Keyword { get; init; }
    public IReadOnlyList<ProjectStatus>? StageStatuses { get; init; }
    public ProjectStatus? ProjectStatus { get; init; }
    public Guid? SalesId { get; init; }
    public Guid? DesignerId { get; init; }
    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtcExclusive { get; init; }
    public bool ExcludeTerminal { get; init; }
}

public sealed class AdminProjectReportCandidateReadModel
{
    public Guid ProjectId { get; init; }
    public string? ProjectCode { get; init; }
    public string ProjectName { get; init; } = string.Empty;
    public ProjectStatus? Status { get; init; }
    public string? BusinessType { get; init; }
    public string? ProjectAddress { get; init; }
    public string? RejectionReason { get; init; }
    public Guid CustomerId { get; init; }
    public string? CustomerName { get; init; }
    public Guid? AssignedSalesId { get; init; }
    public string? AssignedSalesName { get; init; }
    public Guid? AssignedDesignerId { get; init; }
    public string? AssignedDesignerName { get; init; }
    public DateTime? SubmittedAt { get; init; }
    public DateTime? SalesAssignedAt { get; init; }
    public DateTime? ApprovedAt { get; init; }
    public DateTime? DesignerAssignedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public DateTime? RejectedAt { get; init; }
    public DateTime? CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public PaymentStatus? ProjectStartFeeStatus { get; init; }
    public DateTime? ActivePaymentCreatedAt { get; init; }
    public PaymentStatus? ActivePaymentStatus { get; init; }
    public PaymentType? ActivePaymentType { get; init; }
    public bool HasExpiredCollectiblePayment { get; init; }
    public int QuotationRevisionRequestedCount { get; init; }
    public Guid? LatestQuotationId { get; init; }
    public Guid? LatestOrderId { get; init; }
    public OrderStatus? LatestOrderStatus { get; init; }
    public decimal? LatestOrderRemainingAmount { get; init; }
    public Guid? LatestProductionRequestId { get; init; }
    public int CancelledProductionItemCount { get; init; }
    public bool HasOverdueMeasurementSchedule { get; init; }
    public bool HasOverdueDeliverySchedule { get; init; }
}
