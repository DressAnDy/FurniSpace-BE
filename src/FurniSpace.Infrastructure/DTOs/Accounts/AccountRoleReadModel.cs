namespace FurniSpace.Infrastructure.DTOs.Accounts;

public sealed class AccountRoleReadModel
{
    public Guid RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string? Description { get; set; }
}
