namespace FurniSpace.Application.DTOs.Dashboard;

public sealed class DashboardQueueItemLinksDto
{
    public Guid? VersionId { get; set; }

    public Guid? CustomizationRequestId { get; set; }

    public Guid? ProjectId { get; set; }

    public Guid? ProductionRequestId { get; set; }

    public Guid? OrderId { get; set; }
}

public sealed class DashboardQueueItemDto
{
    public string Id { get; set; } = string.Empty;

    public string? WorkType { get; set; }

    public string? EntityId { get; set; }

    public Guid ProjectId { get; set; }

    public string? ProjectCode { get; set; }

    public string ProjectName { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;

    public string? AssigneeName { get; set; }

    public string Group { get; set; } = string.Empty;

    public string Phase { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string Priority { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string ActionPath { get; set; } = string.Empty;

    public DashboardQueueItemLinksDto? Links { get; set; }

    public DateTime? DueAt { get; set; }

    public string? DueBucket { get; set; }

    public string? Warning { get; set; }

    public DateTime LastUpdatedAt { get; set; }
}
