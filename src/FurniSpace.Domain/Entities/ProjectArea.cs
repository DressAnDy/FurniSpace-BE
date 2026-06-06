using System;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Domain.Entities;

public class ProjectArea
{
    public Guid ProjectAreaId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? ParentAreaId { get; set; }
    public string AreaName { get; set; } = null!;
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
    public Guid? CreatedBy { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

