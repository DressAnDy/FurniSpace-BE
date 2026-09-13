namespace FurniSpace.Application.DTOs.Identity;

public sealed class VerifyEmailRequestDto
{
    public string Email { get; set; } = string.Empty;
    public string OtpCode { get; set; } = string.Empty;
}
