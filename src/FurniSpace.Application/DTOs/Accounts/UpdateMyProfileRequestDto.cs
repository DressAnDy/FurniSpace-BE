namespace FurniSpace.Application.DTOs.Accounts;

public sealed class UpdateMyProfileRequestDto
{
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
}
