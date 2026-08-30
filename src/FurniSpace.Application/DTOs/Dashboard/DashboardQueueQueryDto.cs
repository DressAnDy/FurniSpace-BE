namespace FurniSpace.Application.DTOs.Dashboard;

public sealed class DashboardQueueQueryDto
{
    public string Scope { get; set; } = "mine";

    public string? Group { get; set; }

    public string? DateRange { get; set; }

    public string? Priority { get; set; }

    /// <summary>CUSTOMIZATION_REVIEW | PRODUCTION_REQUEST | DELIVERY</summary>
    public string? WorkType { get; set; }

    /// <summary>Status filter; semantics depend on <see cref="WorkType"/>.</summary>
    public string? Status { get; set; }

    /// <summary>OVERDUE | TODAY | THIS_WEEK | LATER</summary>
    public string? DueBucket { get; set; }

    public string? Search { get; set; }

    public int Page { get; set; } = 1;

    public int Limit { get; set; } = 20;
}
