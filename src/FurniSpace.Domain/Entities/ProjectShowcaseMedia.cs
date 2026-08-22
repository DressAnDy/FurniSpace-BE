using System;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Domain.Entities;

public class ProjectShowcaseMedia
{
    public Guid ProjectShowcaseMediaId { get; set; }
    public Guid ProjectShowcaseId { get; set; }
    public Guid FileId { get; set; }
    public ProjectShowcaseMediaType MediaType { get; set; }
    public string? Title { get; set; }
    public string? Caption { get; set; }
    public bool IsCover { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
