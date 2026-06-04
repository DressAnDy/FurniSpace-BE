namespace FurniSpace.Application.DTOs.Identity;

public sealed class UpdateProfileRequestDto
{
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
}
