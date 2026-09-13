using System;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Domain.Entities;

public class ProjectChat
{
    public Guid ChatId { get; set; }
    public Guid ProjectId { get; set; }
    public ProjectChatType ChatType { get; set; }
    public Guid? StaffId { get; set; }
    public string? Title { get; set; }
    public ProjectChatStatus? Status { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
}

