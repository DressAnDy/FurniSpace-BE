namespace FurniSpace.Application.DTOs.Dashboard;

public sealed class DesignerDashboardKpisDto
{
    public int MeasurementDue { get; set; }

    public int ProposalsInProgress { get; set; }

    public int RevisionRequested { get; set; }

    public int OverdueTasks { get; set; }
}
