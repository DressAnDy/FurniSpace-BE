namespace FurniSpace.Domain.Common;

public abstract class BaseEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; protected set; }
    public bool IsDeleted { get; protected set; }
    public byte[] RowVersion { get; protected set; } = Array.Empty<byte>();

    public void SoftDelete() => IsDeleted = true;

    protected void SetUpdatedAt() => UpdatedAt = DateTime.UtcNow;
}
