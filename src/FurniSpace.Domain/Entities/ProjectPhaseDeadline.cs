using System;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Domain.Entities;

public class ProjectPhaseDeadline
{
    public Guid ProjectPhaseDeadlineId { get; set; }
    public Guid ProjectId { get; set; }
    public ProjectPhaseType Phase { get; set; }
    public DateOnly DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
