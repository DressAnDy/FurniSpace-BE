namespace FurniSpace.Application.DTOs.Accounts;

public sealed class AccountRoleDto
{
    public Guid RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string? Description { get; set; }
}
