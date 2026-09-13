using System;

namespace FurniSpace.Domain.Entities;

public class Role
{
    public Guid RoleId { get; set; }
    public string RoleName { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}


