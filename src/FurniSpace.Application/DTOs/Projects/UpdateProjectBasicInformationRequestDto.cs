namespace FurniSpace.Application.DTOs.Projects;

public sealed class UpdateProjectBasicInformationRequestDto
{
    public string ProjectName { get; set; } = string.Empty;
    public string BusinessType { get; set; } = string.Empty;
    public string? ProjectAddress { get; set; }
    public string? BusinessPurpose { get; set; }
    public string FurnitureRequirement { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal? TotalAreaSqm { get; set; }
    public int? NumberOfFloors { get; set; }
    public decimal? BudgetMin { get; set; }
    public decimal? BudgetMax { get; set; }
    public DateOnly? TargetCompletionDate { get; set; }
}
