using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Dashboard;

public sealed class ProjectPhaseDeadlineRiskQueryDto
{
    public string? Phase { get; set; }

    public string? Status { get; set; }

    public Guid? SalesId { get; set; }

    public Guid? DesignerId { get; set; }

    public DateOnly? From { get; set; }

    public DateOnly? To { get; set; }

    public int Page { get; set; } = 1;

    public int Limit { get; set; } = 20;
}

public sealed class ProjectPhaseDeadlineRiskResponseDto
{
    public List<ProjectPhaseDeadlineRiskItemDto> Items { get; set; } = [];

    public Dictionary<string, int> CountsByGroup { get; set; } = new(StringComparer.Ordinal);

    public int Page { get; set; }

    public int Limit { get; set; }

    public int Total { get; set; }
}

public sealed class ProjectPhaseDeadlineRiskItemDto
{
    public Guid ProjectId { get; set; }

    public string? ProjectCode { get; set; }

    public string ProjectName { get; set; } = string.Empty;

    public ProjectPhaseType Phase { get; set; }

    public DateOnly DueDate { get; set; }

    public DateTime? CompletedAt { get; set; }

    public ProjectStatus? ProjectStatus { get; set; }

    public Guid? AssignedSalesId { get; set; }

    public string? AssignedSalesName { get; set; }

    public Guid? AssignedDesignerId { get; set; }

    public string? AssignedDesignerName { get; set; }

    public Guid? AssignedProductionId { get; set; }

    public string? AssignedProductionName { get; set; }

    public string Status { get; set; } = string.Empty;

    public string Group { get; set; } = string.Empty;

    public int Days { get; set; }
}
