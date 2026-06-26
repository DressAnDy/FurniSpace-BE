using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.Projects;

public sealed class ProjectDetailReadModel
{
    public Guid ProjectId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid? AssignedSalesId { get; set; }
    public Guid? AssignedDesignerId { get; set; }
    public string? ProjectCode { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string? BusinessType { get; set; }
    public string? ProjectAddress { get; set; }
    public string? BusinessPurpose { get; set; }
    public string? FurnitureRequirement { get; set; }
    public string? Description { get; set; }
    public decimal? TotalAreaSqm { get; set; }
    public int? NumberOfFloors { get; set; }
    public decimal? BudgetMin { get; set; }
    public decimal? BudgetMax { get; set; }
    public DateOnly? TargetCompletionDate { get; set; }
    public ProjectStatus? Status { get; set; }
    public DateTime? SubmittedAt { get; set; }
}
