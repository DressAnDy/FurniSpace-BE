using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.DTOs.Projects;

public sealed class ProjectByUserItemReadModel
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
    public ProjectCustomerSummaryReadModel Customer { get; set; } = new();
    public ProjectAccountSummaryReadModel? AssignedSales { get; set; }
    public ProjectAccountSummaryReadModel? AssignedDesigner { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
