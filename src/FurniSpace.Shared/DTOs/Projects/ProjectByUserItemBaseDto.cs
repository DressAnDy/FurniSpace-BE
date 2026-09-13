#nullable enable

namespace FurniSpace.Shared.DTOs.Projects;

public abstract class ProjectByUserItemBaseDto<TStatus, TCustomer, TStaff>
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
    public TStatus Status { get; set; } = default!;
    public TCustomer Customer { get; set; } = default!;
    public TStaff? AssignedSales { get; set; }
    public TStaff? AssignedDesigner { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
