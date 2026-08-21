using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Projects;

public sealed class UpsertProjectPhaseDeadlinesRequestDto
{
    public DateOnly? ProposalDueDate { get; set; }
    public DateOnly? ProductionDueDate { get; set; }
}

public sealed class ProjectPhaseDeadlinePlanDto
{
    public Guid ProjectId { get; set; }
    public DateOnly? TargetCompletionDate { get; set; }
    public List<ProjectPhaseDeadlineItemDto> Deadlines { get; set; } = [];
}

public sealed class ProjectPhaseDeadlineItemDto
{
    public ProjectPhaseType Phase { get; set; }
    public DateOnly DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public int OverdueDays { get; set; }
}
