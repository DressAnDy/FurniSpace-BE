#nullable enable

using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.Production;

public sealed class AvailableProductionStaffReadModel
{
    public Guid AccountId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public AccountStatus? AccountStatus { get; set; }
    public int ActiveRequestCount { get; set; }
    public int PendingReviewRequestCount { get; set; }
    public int InProductionRequestCount { get; set; }
    public int BlockedRequestCount { get; set; }
    public bool IsAvailable { get; set; }
}
