using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using FurniSpace.Application.Common.Auth;
using FurniSpace.Application.DTOs;
using FurniSpace.Application.Interfaces.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FurniSpace.Application.Services.Identity;

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtSettings _settings;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();

    public JwtTokenService(IOptions<JwtSettings> settings)
    {
        _settings = settings.Value;
    }

    public AuthResponseDto CreateToken(Guid userId, string email, string fullName, IEnumerable<string>? roles = null)
    {
        var now = DateTimeOffset.UtcNow;
        var accessTokenExpiresAt = now.AddMinutes(_settings.AccessTokenExpirationMinutes);
        var refreshTokenExpiresAt = now.AddDays(_settings.RefreshTokenExpirationDays);
        var jti = Guid.NewGuid().ToString("N");

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Name, fullName),
            new(JwtRegisteredClaimNames.Jti, jti),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Name, fullName)
        };

        if (roles is not null)
        {
            claims.AddRange(roles.Where(role => !string.IsNullOrWhiteSpace(role))
                .Select(role => new Claim(ClaimTypes.Role, role)));
        }

        var signingCredentials = new SigningCredentials(GetSecurityKey(), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: accessTokenExpiresAt.UtcDateTime,
            signingCredentials: signingCredentials);

        return new AuthResponseDto
        {
            AccessToken = _tokenHandler.WriteToken(token),
            RefreshToken = CreateRefreshToken(),
            AccessTokenExpiresAt = accessTokenExpiresAt,
            RefreshTokenExpiresAt = refreshTokenExpiresAt
        };
    }

    public ClaimsPrincipal? GetPrincipalFromExpiredToken(string accessToken)
    {
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = GetSecurityKey(),
            ValidateIssuer = true,
            ValidIssuer = _settings.Issuer,
            ValidateAudience = true,
            ValidAudience = _settings.Audience,
            ValidateLifetime = false,
            ClockSkew = TimeSpan.Zero
        };

        try
        {
            return _tokenHandler.ValidateToken(accessToken, validationParameters, out _);
        }
        catch
        {
            return null;
        }
    }

    public string? GetJti(string accessToken)
    {
        var token = _tokenHandler.ReadJwtToken(accessToken);
        return token.Claims.FirstOrDefault(claim => claim.Type == JwtRegisteredClaimNames.Jti)?.Value;
    }

    private SymmetricSecurityKey GetSecurityKey()
    {
        return new SymmetricSecurityKey(_settings.GetSecretKeyBytes());
    }

    private static string CreateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }
}
