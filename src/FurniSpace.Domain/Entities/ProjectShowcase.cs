using System;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Domain.Entities;

public class ProjectShowcase
{
    public Guid ProjectShowcaseId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? FeaturedReviewId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? Description { get; set; }
    public ProjectShowcaseStatus Status { get; set; } = ProjectShowcaseStatus.DRAFT;
    public Guid? CreatedBy { get; set; }
    public Guid? ApprovedBy { get; set; }
    public Guid? PublishedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
