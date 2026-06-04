using FurniSpace.Application.DTOs;
using FurniSpace.Application.Interfaces.Identity;

namespace FurniSpace.Application.Services.Identity;

public sealed class AuthService : IAuthService
{
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRefreshTokenStore _refreshTokenStore;

    public AuthService(IJwtTokenService jwtTokenService, IRefreshTokenStore refreshTokenStore)
    {
        _jwtTokenService = jwtTokenService;
        _refreshTokenStore = refreshTokenStore;
    }

    public async Task<AuthResponseDto> CreateSessionAsync(
        Guid userId,
        string email,
        string fullName,
        IEnumerable<string>? roles = null,
        CancellationToken cancellationToken = default)
    {
        var token = _jwtTokenService.GenerateTokenPair(userId, email, fullName, roles);
        await _refreshTokenStore.StoreAsync(userId, token.RefreshToken, token.RefreshTokenExpiresAt, cancellationToken);

        return token;
    }

    public Task<bool> ValidateRefreshTokenAsync(Guid userId, string refreshToken, CancellationToken cancellationToken = default)
    {
        return _refreshTokenStore.ExistsAsync(userId, refreshToken, cancellationToken);
    }

    public async Task<AuthResponseDto?> RotateRefreshTokenAsync(
        Guid userId,
        string refreshToken,
        string email,
        string fullName,
        IEnumerable<string>? roles = null,
        CancellationToken cancellationToken = default)
    {
        var isValid = await _refreshTokenStore.ExistsAsync(userId, refreshToken, cancellationToken);
        if (!isValid)
        {
            return null;
        }

        await _refreshTokenStore.RevokeAsync(userId, refreshToken, cancellationToken);
        return await CreateSessionAsync(userId, email, fullName, roles, cancellationToken);
    }

    public Task RevokeRefreshTokenAsync(Guid userId, string refreshToken, CancellationToken cancellationToken = default)
    {
        return _refreshTokenStore.RevokeAsync(userId, refreshToken, cancellationToken);
    }

    public Task RevokeAccessTokenAsync(string jti, DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
    {
        return _refreshTokenStore.RevokeAccessTokenAsync(jti, expiresAt, cancellationToken);
    }

    public Task<bool> IsAccessTokenRevokedAsync(string jti, CancellationToken cancellationToken = default)
    {
        return _refreshTokenStore.IsAccessTokenRevokedAsync(jti, cancellationToken);
    }
}
