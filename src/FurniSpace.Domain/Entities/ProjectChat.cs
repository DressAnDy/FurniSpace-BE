using System;

namespace FurniSpace.Domain.Entities;

public class ProjectChat
{
    public Guid ChatId { get; set; }
    public Guid ProjectId { get; set; }
    public string ChatType { get; set; } = null!;
    public Guid? StaffId { get; set; }
    public string? Title { get; set; }
    public string? Status { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
}


