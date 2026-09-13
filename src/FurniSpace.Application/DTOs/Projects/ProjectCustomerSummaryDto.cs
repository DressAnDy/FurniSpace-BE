namespace FurniSpace.Application.DTOs.Projects;

public sealed class ProjectCustomerSummaryDto
{
    public Guid AccountId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
}
