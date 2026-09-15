namespace FurniSpace.Application.DTOs.Dashboard;

/// <summary>
/// Designer dashboard KPI cards.
/// <c>measurementDue</c> / <c>confirmedMeasurements</c> count CONFIRMED MEASUREMENT schedules
/// whose <c>scheduledStart</c> falls in <c>dateRange</c> (Asia/Ho_Chi_Minh; week = Mon–Sun).
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

    public int ProposalsInProgress { get; set; }

    public int RevisionRequested { get; set; }

    public int OverdueTasks { get; set; }
}
