using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.ProjectAreas;

public sealed class UpdateProjectAreaRequestDto
{
    public Guid? ParentAreaId { get; set; }
    public string? AreaName { get; set; }
    public ProjectAreaType? AreaType { get; set; }
    public int? FloorNumber { get; set; }
    public string? Description { get; set; }
    public decimal? AreaSqm { get; set; }
    public decimal? Width { get; set; }
    public decimal? Length { get; set; }
    public decimal? Height { get; set; }
    public string? CurrentCondition { get; set; }
    public string? RequirementNote { get; set; }
    public ProjectAreaStatus? Status { get; set; }
}
