namespace FurniSpace.Infrastructure.DTOs.Projects;

public sealed class DesignerAccountReadModel
{
    public Guid AccountId { get; set; }
    public string FullName { get; set; } = string.Empty;
}
