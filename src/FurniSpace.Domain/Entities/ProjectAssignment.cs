using System;

namespace FurniSpace.Domain.Entities;

public class ProjectAssignment
{
    public Guid AssignmentId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid AccountId { get; set; }
    public string AssignmentRole { get; set; } = null!;
    public string? Status { get; set; }
    public Guid? AssignedBy { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime? UnassignedAt { get; set; }
    public string? Note { get; set; }
}


