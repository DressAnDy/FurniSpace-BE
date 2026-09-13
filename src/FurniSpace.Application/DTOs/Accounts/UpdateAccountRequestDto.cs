namespace FurniSpace.Application.DTOs.Accounts;

public sealed class UpdateAccountRequestDto
{
    public Guid RoleId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Status { get; set; }
}
