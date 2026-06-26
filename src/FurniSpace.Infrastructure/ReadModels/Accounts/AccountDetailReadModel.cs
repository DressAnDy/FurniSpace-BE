using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.Accounts;

public sealed class AccountDetailReadModel
{
    public Guid AccountId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? AvatarUrl { get; set; }
    public AccountRoleReadModel Role { get; set; } = new();
    public AccountStatus? Status { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
