namespace FurniSpace.Application.DTOs.Dashboard;

/// <summary>
/// Designer dashboard KPI cards.
/// <c>measurementDue</c> / <c>confirmedMeasurements</c> count CONFIRMED MEASUREMENT schedules
/// whose <c>scheduledStart</c> falls in <c>dateRange</c> (Asia/Ho_Chi_Minh; week = Mon–Sun).
/// <c>proposalsInProgress</c> / <c>proposalConsultingProjects</c> count projects in
/// <c>PROPOSAL_CONSULTING</c> whose <c>updatedAt</c> (fallback <c>createdAt</c>) falls in <c>dateRange</c>.
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

    public int RevisionRequested { get; set; }

    public int OverdueTasks { get; set; }
}
