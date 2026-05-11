using System;

namespace FurniSpace.Domain.Common;

public abstract class AuditableEntity : BaseEntity
{
    public new DateTime CreatedAt { get; set; }
    public new DateTime UpdatedAt { get; set; }
}
