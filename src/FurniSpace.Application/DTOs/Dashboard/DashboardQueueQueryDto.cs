namespace FurniSpace.Application.DTOs.Dashboard;

public sealed class DashboardQueueQueryDto
{
    public string Scope { get; set; } = "mine";

    public string? Group { get; set; }

    public string? DateRange { get; set; }

    public string? Priority { get; set; }

    public string? Search { get; set; }

    public int Page { get; set; } = 1;

    public int Limit { get; set; } = 20;
}
