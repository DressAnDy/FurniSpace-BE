namespace FurniSpace.Application.DTOs.Accounts;

public sealed class AvailableDesignerDto
{
    public Guid AccountId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Status { get; set; }

    /// <summary>
    /// Projects in DESIGN_ACTIVE statuses (occupies capacity slot).
    /// </summary>
    public int DesignActiveCount { get; set; }

    /// <summary>
    /// Non-terminal projects still assigned to this designer.
    /// </summary>
    public int LifecycleAssignedCount { get; set; }

    /// <summary>
    /// Backward-compatible alias of <see cref="DesignActiveCount"/> (Sales assign picker).
    /// </summary>
    public int CurrentActiveProjectCount { get; set; }

    public int MaxActiveProjects { get; set; }
    public int AvailableSlot { get; set; }

    /// <summary>
    /// AVAILABLE | FULL | OVER — based on DesignActiveCount vs MaxActiveProjects.
    /// </summary>
    public string CapacityState { get; set; } = string.Empty;

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
