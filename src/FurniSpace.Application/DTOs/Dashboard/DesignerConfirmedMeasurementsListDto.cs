namespace FurniSpace.Application.DTOs.Dashboard;

public sealed class DesignerConfirmedMeasurementsListResponseDto
{
    public IReadOnlyList<DesignerConfirmedMeasurementItemDto> Items { get; set; } = [];

    public int Page { get; set; }

    public int Limit { get; set; }

    public int Total { get; set; }
}

public sealed class DesignerConfirmedMeasurementItemDto
{
    public Guid ScheduleId { get; set; }

    public Guid ProjectId { get; set; }

    public string? ProjectCode { get; set; }

    public string ProjectName { get; set; } = string.Empty;

    public string? Title { get; set; }

    public DateTime ScheduledStart { get; set; }

    public DateTime? ScheduledEnd { get; set; }

    public string? Location { get; set; }

    public string Status { get; set; } = string.Empty;

    public Guid? AssignedStaffId { get; set; }

    public string? AssignedStaffName { get; set; }
}
