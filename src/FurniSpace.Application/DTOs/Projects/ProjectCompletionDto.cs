#nullable enable

namespace FurniSpace.Application.DTOs.Projects;

public sealed class ProjectCompletionDto
{
    public Guid ProjectId { get; set; }
    public string ProjectStatus { get; set; } = string.Empty;
    public DateTime CompletedAt { get; set; }
}
