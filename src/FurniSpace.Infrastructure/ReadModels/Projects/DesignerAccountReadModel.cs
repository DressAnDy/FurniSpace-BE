namespace FurniSpace.Infrastructure.ReadModels.Projects;

public sealed class DesignerAccountReadModel
{
    public Guid AccountId { get; set; }
    public string FullName { get; set; } = string.Empty;
}
