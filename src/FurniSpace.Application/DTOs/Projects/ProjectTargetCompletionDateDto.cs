namespace FurniSpace.Application.DTOs.Projects;

public sealed class ProjectTargetCompletionDateDto
{
    public Guid ProjectId { get; set; }
    public DateOnly? TargetCompletionDate { get; set; }
    public DateTime UpdatedAt { get; set; }
}
