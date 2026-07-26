#nullable enable

namespace FurniSpace.Application.DTOs.Production;

public sealed class AvailableProductionStaffDto
{
    public Guid AccountId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string AccountStatus { get; set; } = string.Empty;
    public int ActiveRequestCount { get; set; }
    public int PendingReviewRequestCount { get; set; }
    public int InProductionRequestCount { get; set; }
    public int BlockedRequestCount { get; set; }
    public bool IsAvailable { get; set; }
}
