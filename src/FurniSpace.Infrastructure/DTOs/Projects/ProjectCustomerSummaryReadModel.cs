namespace FurniSpace.Infrastructure.DTOs.Projects;

public sealed class ProjectCustomerSummaryReadModel
{
    public Guid AccountId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
}
