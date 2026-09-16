namespace FurniSpace.Application.DTOs.Dashboard;

/// <summary>
/// Designer dashboard KPI cards.
/// <c>measurementDue</c> / <c>confirmedMeasurements</c> count CONFIRMED MEASUREMENT schedules
/// whose <c>scheduledStart</c> falls in <c>dateRange</c> (Asia/Ho_Chi_Minh; week = Mon–Sun).
/// <c>proposalsInProgress</c> / <c>proposalConsultingProjects</c> count projects in
/// <c>PROPOSAL_CONSULTING</c> whose <c>updatedAt</c> (fallback <c>createdAt</c>) falls in <c>dateRange</c>.
/// <c>assignedProjects</c> is a stock count of designer-assigned projects (excludes
/// <c>COMPLETED</c>/<c>REJECTED</c>; ignores <c>dateRange</c>). <c>overdueTasks</c> is deprecated.
/// </summary>
public sealed class DesignerDashboardKpisDto
{
    /// <summary>
    /// Confirmed measurement schedules in scope + dateRange.
    /// Alias of <see cref="ConfirmedMeasurements"/> kept for FE compatibility.
    /// </summary>
    public int MeasurementDue { get; set; }

    /// <summary>Same value as <see cref="MeasurementDue"/>.</summary>
    public int ConfirmedMeasurements { get; set; }

    /// <summary>
    /// Projects with status <c>PROPOSAL_CONSULTING</c> in scope + dateRange.
    /// Does not count proposal DRAFT/PUBLISHED entities or <c>SPACE_VERIFIED</c>.
    /// </summary>
    public int ProposalsInProgress { get; set; }

    /// <summary>Same value as <see cref="ProposalsInProgress"/>.</summary>
    public int ProposalConsultingProjects { get; set; }

    /// <summary>
    /// Proposals with status <c>REVISION_REQUESTED</c> on designer-scoped projects.
    /// Does not count project <c>QUOTATION_REVISION_REQUESTED</c>.
    /// </summary>
    public int RevisionRequested { get; set; }

    /// <summary>Same value as <see cref="RevisionRequested"/>.</summary>
    public int ProposalRevisionsRequested { get; set; }

    /// <summary>
    /// Stock count of projects currently assigned to the scoped designer.
    /// Excludes <c>COMPLETED</c> and <c>REJECTED</c>. Ignores <c>dateRange</c>.
    /// </summary>
    public int AssignedProjects { get; set; }

    /// <summary>
    /// Deprecated. Projects with <c>targetCompletionDate</c> before today UTC.
    /// Prefer <see cref="AssignedProjects"/> for the Assigned Projects card.
    /// </summary>
    public int OverdueTasks { get; set; }
}
