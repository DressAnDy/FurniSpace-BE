using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.Dashboard;

public sealed class DashboardQueueFilterReadModel
{
    public string Scope { get; init; } = "mine";

    public Guid CurrentUserId { get; init; }

    public string? CurrentUserRole { get; init; }

    public string? Search { get; init; }

    public string? DateRange { get; init; }

    public DateTime UtcNow { get; init; }
}

public sealed class ProjectPhaseDeadlineRiskQueryReadModel
{
    public ProjectPhaseType? Phase { get; init; }

    public Guid? SalesId { get; init; }

    public Guid? DesignerId { get; init; }

    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }
}

public sealed class ProjectPhaseDeadlineRiskRowReadModel
{
    public Guid ProjectId { get; init; }

    public string? ProjectCode { get; init; }

    public string ProjectName { get; init; } = string.Empty;

    public ProjectPhaseType Phase { get; init; }

    public DateOnly DueDate { get; init; }

    public DateTime? CompletedAt { get; init; }

    public ProjectStatus? ProjectStatus { get; init; }

    public Guid? AssignedSalesId { get; init; }

    public string? AssignedSalesName { get; init; }

    public Guid? AssignedDesignerId { get; init; }

    public string? AssignedDesignerName { get; init; }

    public Guid? AssignedProductionId { get; init; }

    public string? AssignedProductionName { get; init; }
}

public sealed class DashboardProjectQueueRowReadModel
{
    public Guid ProjectId { get; init; }

    public string? ProjectCode { get; init; }

    public string ProjectName { get; init; } = string.Empty;

    public ProjectStatus? Status { get; init; }

    public string CustomerName { get; init; } = string.Empty;

    public Guid? AssignedSalesId { get; init; }

    public string? AssignedSalesName { get; init; }

    public Guid? AssignedDesignerId { get; init; }

    public string? AssignedDesignerName { get; init; }

    public DateOnly? TargetCompletionDate { get; init; }

    public DateTime? UpdatedAt { get; init; }

    public DateTime? SubmittedAt { get; init; }

    public DateTime? CreatedAt { get; init; }

    public Guid? OrderId { get; init; }

    public OrderStatus? OrderStatus { get; init; }

    public decimal? RemainingAmount { get; init; }

    public DateTime? CustomerConfirmedDeliveryAt { get; init; }

    public DateTime? OrderUpdatedAt { get; init; }
}

public sealed class DashboardProductionQueueRowReadModel
{
    public Guid ProductionRequestId { get; init; }

    public string? ProductionCode { get; init; }

    public Guid ProjectId { get; init; }

    public string? ProjectCode { get; init; }

    public string ProjectName { get; init; } = string.Empty;

    public string CustomerName { get; init; } = string.Empty;

    public Guid? AssignedTo { get; init; }

    public string? AssignedToName { get; init; }

    public ProductionRequestStatus Status { get; init; }

    public string? Priority { get; init; }

    public DateOnly? ProductionDeadline { get; init; }

    public int BlockedItemCount { get; init; }

    public DateTime? UpdatedAt { get; init; }

    public DateTime? CreatedAt { get; init; }
}

public sealed class SalesDashboardKpisReadModel
{
    public int NewRequests { get; init; }

    public int WaitingCustomer { get; init; }

    public int PaymentFollowUp { get; init; }

    public int OverdueTasks { get; init; }

    public int ActiveProjects { get; init; }
}

public sealed class DesignerDashboardKpisReadModel
{
    public int MeasurementDue { get; init; }

    public int ProposalsInProgress { get; init; }

    public int RevisionRequested { get; init; }

    public int OverdueTasks { get; init; }
}

public sealed class ProductionDashboardKpisReadModel
{
    public int PendingReview { get; init; }

    public int InProduction { get; init; }

    public int ReadyToComplete { get; init; }

    public int OverdueTasks { get; init; }
}
