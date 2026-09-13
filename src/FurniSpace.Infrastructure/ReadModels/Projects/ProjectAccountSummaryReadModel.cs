namespace FurniSpace.Infrastructure.ReadModels.Projects;

public sealed class ProjectAccountSummaryReadModel
{
    public Guid AccountId { get; set; }
    public string FullName { get; set; } = string.Empty;
}
