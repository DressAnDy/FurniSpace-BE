using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.Accounts;

public sealed class AvailableDesignerReadModel
{
    public Guid AccountId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? AvatarUrl { get; set; }
    public AccountStatus? Status { get; set; }
    public int DesignActiveCount { get; set; }
    public int LifecycleAssignedCount { get; set; }

    /// <summary>
    /// Alias of <see cref="DesignActiveCount"/> for older callers / sort.
    /// </summary>
    public int CurrentActiveProjectCount { get; set; }

    public int MaxActiveProjects { get; set; }
    public int AvailableSlot { get; set; }
    public string CapacityState { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
