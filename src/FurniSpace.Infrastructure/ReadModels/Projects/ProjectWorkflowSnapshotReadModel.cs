#nullable enable

using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.Projects;

public sealed record ProjectWorkflowSnapshotReadModel
{
    public Guid ProjectId { get; init; }
    public string? ProjectCode { get; init; }
    public string ProjectName { get; init; } = string.Empty;
    public ProjectStatus? Status { get; init; }
    public string? BusinessType { get; init; }
    public DateTime? SubmittedAt { get; init; }
    public DateTime? SalesAssignedAt { get; init; }
    public DateTime? DesignerAssignedAt { get; init; }
    public DateTime? RejectedAt { get; init; }

    public Guid CustomerId { get; init; }
    public string? CustomerName { get; init; }
    public Guid? AssignedSalesId { get; init; }
    public string? SalesName { get; init; }
    public Guid? AssignedDesignerId { get; init; }
    public string? DesignerName { get; init; }

    public IReadOnlyList<ProjectWorkflowProposalReadModel> Proposals { get; init; } = [];
    public IReadOnlyList<ProjectWorkflowQuotationReadModel> Quotations { get; init; } = [];
    public IReadOnlyList<ProjectWorkflowOrderReadModel> Orders { get; init; } = [];
    public IReadOnlyList<ProjectWorkflowOrderItemReadModel> OrderItems { get; init; } = [];
    public IReadOnlyList<ProjectWorkflowProductionRequestReadModel> ProductionRequests { get; init; } = [];
    public IReadOnlyList<ProjectWorkflowProductionItemReadModel> ProductionItems { get; init; } = [];
    public IReadOnlyList<ProjectWorkflowScheduleReadModel> Schedules { get; init; } = [];
    public IReadOnlyList<ProjectWorkflowPaymentReadModel> Payments { get; init; } = [];
}

public sealed record ProjectWorkflowProposalReadModel
{
    public Guid ProposalId { get; init; }
    public string ProposalName { get; init; } = string.Empty;
    public ProposalStatus? Status { get; init; }
    public int? VersionNo { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public DateTime? SelectedAt { get; init; }
}

public sealed record ProjectWorkflowQuotationReadModel
{
    public Guid QuotationId { get; init; }
    public string QuotationCode { get; init; } = string.Empty;
    public QuotationStatus? Status { get; init; }
    public decimal? TotalAmount { get; init; }
    public DateTime? SentAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed record ProjectWorkflowOrderReadModel
{
    public Guid OrderId { get; init; }
    public string OrderCode { get; init; } = string.Empty;
    public OrderStatus? Status { get; init; }
    public decimal? RemainingAmount { get; init; }
    public DateTime? CreatedAt { get; init; }
}

public sealed record ProjectWorkflowOrderItemReadModel
{
    public Guid OrderId { get; init; }
    public int? Quantity { get; init; }
    public int? DeliveredQuantity { get; init; }
}

public sealed record ProjectWorkflowProductionRequestReadModel
{
    public Guid ProductionRequestId { get; init; }
    public string? ProductionCode { get; init; }
    public ProductionRequestStatus? Status { get; init; }
    public DateOnly? EstimatedCompletionDate { get; init; }
    public Guid? AssignedTo { get; init; }
    public string? AssignedToName { get; init; }
    public DateTime? CreatedAt { get; init; }
}

public sealed record ProjectWorkflowProductionItemReadModel
{
    public Guid ProductionRequestId { get; init; }
    public ProductionItemStatus? Status { get; init; }
    public DateOnly? EstimatedCompletionDate { get; init; }
}

public sealed record ProjectWorkflowScheduleReadModel
{
    public Guid ScheduleId { get; init; }
    public string? Title { get; init; }
    public ProjectScheduleType? ScheduleType { get; init; }
    public ProjectScheduleStatus? Status { get; init; }
    public DateTime ScheduledStart { get; init; }
    public DateTime? ScheduledEnd { get; init; }
}

public sealed record ProjectWorkflowPaymentReadModel
{
    public Guid PaymentId { get; init; }
    public string PaymentCode { get; init; } = string.Empty;
    public PaymentType? PaymentType { get; init; }
    public PaymentStatus? Status { get; init; }
    public DateTime? CreatedAt { get; init; }
}
