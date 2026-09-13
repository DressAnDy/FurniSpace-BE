namespace FurniSpace.Application.DTOs.Projects;

public sealed class ProjectAccountSummaryDto
{
    public Guid AccountId { get; set; }
    public string FullName { get; set; } = string.Empty;
}
