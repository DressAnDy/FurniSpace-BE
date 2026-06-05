using FurniSpace.Application.DTOs;
using FurniSpace.Application.Common.Auth;
using FurniSpace.Application.Interfaces.Identity;
using Microsoft.Extensions.Options;

namespace FurniSpace.Application.Services.Identity;

public sealed class AuthService : IAuthService
{
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRefreshTokenStore _refreshTokenStore;
    private readonly JwtSettings _jwtSettings;

    public AuthService(
        IJwtTokenService jwtTokenService,
        IRefreshTokenStore refreshTokenStore,
        IOptions<JwtSettings> jwtSettings)
    {
        _jwtTokenService = jwtTokenService;
        _refreshTokenStore = refreshTokenStore;
        _jwtSettings = jwtSettings.Value;
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
        var consumed = await _refreshTokenStore.ConsumeAsync(userId, refreshToken, cancellationToken);
        if (!consumed)
        {
            await _refreshTokenStore.RevokeAllAsync(userId, cancellationToken);
            return null;
        }

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

    public Task RevokeUserAccessTokensAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var ttl = TimeSpan.FromMinutes(_jwtSettings.AccessTokenExpirationMinutes + 1);
        return _refreshTokenStore.RevokeUserAccessTokensAsync(userId, DateTimeOffset.UtcNow, ttl, cancellationToken);
    }

    public Task<bool> IsAccessTokenRevokedAsync(string jti, CancellationToken cancellationToken = default)
    {
        return _refreshTokenStore.IsAccessTokenRevokedAsync(jti, cancellationToken);
    }

    public async Task<bool> IsAccessTokenRevokedAsync(
        string jti,
        Guid userId,
        DateTimeOffset issuedAt,
        CancellationToken cancellationToken = default)
    {
        return await _refreshTokenStore.IsAccessTokenRevokedAsync(jti, cancellationToken) ||
            await _refreshTokenStore.AreUserAccessTokensRevokedAsync(userId, issuedAt, cancellationToken);
    }
}
