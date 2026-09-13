#nullable enable

namespace FurniSpace.Shared.DTOs.ProjectAreas;

public abstract class ProjectAreaBaseDto<TAreaType, TStatus>
{
    public Guid ProjectAreaId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? ParentAreaId { get; set; }
    public string AreaName { get; set; } = string.Empty;
    public TAreaType AreaType { get; set; } = default!;
    public int? FloorNumber { get; set; }
    public bool IsSpecialLayout { get; set; }
    public string? Description { get; set; }
    public decimal? AreaSqm { get; set; }
    public decimal? Width { get; set; }
    public decimal? Length { get; set; }
    public decimal? Height { get; set; }
    public string? CurrentCondition { get; set; }
    public string? RequirementNote { get; set; }
    public TStatus Status { get; set; } = default!;
    public Guid? CreatedBy { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
