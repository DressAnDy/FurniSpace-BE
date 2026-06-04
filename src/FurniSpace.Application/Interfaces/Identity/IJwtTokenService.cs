using System.Security.Claims;
using FurniSpace.Application.DTOs;

namespace FurniSpace.Application.Interfaces.Identity;

public interface IJwtTokenService
{
    AuthResponseDto GenerateTokenPair(
        Guid userId,
        string email,
        string fullName,
        IEnumerable<string>? roles = null);

    ClaimsPrincipal? GetPrincipalFromExpiredToken(string accessToken);
    string? GetJti(string accessToken);
}
