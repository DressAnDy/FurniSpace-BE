namespace FurniSpace.Application.DTOs.Projects;

public sealed class AssignedDesignerDto
{
    public Guid AccountId { get; set; }
    public string FullName { get; set; } = string.Empty;
}
