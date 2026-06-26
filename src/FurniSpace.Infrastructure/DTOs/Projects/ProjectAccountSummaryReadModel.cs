namespace FurniSpace.Infrastructure.DTOs.Projects;

public sealed class ProjectAccountSummaryReadModel
{
    public Guid AccountId { get; set; }
    public string FullName { get; set; } = string.Empty;
}
