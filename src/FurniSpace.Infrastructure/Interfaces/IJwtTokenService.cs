using System.Security.Claims;

namespace FurniSpace.Infrastructure.Interfaces;

public interface IJwtTokenService
{
    JwtTokenResult CreateToken(
        Guid userId,
        string email,
        string fullName,
        IEnumerable<string>? roles = null);

    ClaimsPrincipal? GetPrincipalFromExpiredToken(string accessToken);
    string? GetJti(string accessToken);
}
