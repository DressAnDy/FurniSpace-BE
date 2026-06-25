using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Projects;

public sealed class ProjectByUserItemDto
{
    public Guid ProjectId { get; set; }
    public string? ProjectCode { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string? BusinessType { get; set; }
    public string? ProjectAddress { get; set; }
    public decimal? TotalAreaSqm { get; set; }
    public int? NumberOfFloors { get; set; }
    public decimal? BudgetMin { get; set; }
    public decimal? BudgetMax { get; set; }
    public DateOnly? TargetCompletionDate { get; set; }
    public ProjectStatus? Status { get; set; }
    public ProjectCustomerSummaryDto Customer { get; set; } = new();
    public ProjectAccountSummaryDto? AssignedSales { get; set; }
    public ProjectAccountSummaryDto? AssignedDesigner { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
