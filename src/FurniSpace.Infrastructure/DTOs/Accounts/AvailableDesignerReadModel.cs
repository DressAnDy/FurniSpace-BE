using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.DTOs.Accounts;

public sealed class AvailableDesignerReadModel
{
    public Guid AccountId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? AvatarUrl { get; set; }
    public AccountStatus? Status { get; set; }
    public int CurrentActiveProjectCount { get; set; }
    public int MaxActiveProjects { get; set; }
    public int AvailableSlot { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
