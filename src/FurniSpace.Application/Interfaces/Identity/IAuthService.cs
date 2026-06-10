namespace FurniSpace.Application.Interfaces.Identity;

using FurniSpace.Application.DTOs.Auth;

public interface IAuthService
{
    Task<AuthResponseDto> CreateSessionAsync(
        Guid userId,
        string email,
        string fullName,
        IEnumerable<string>? roles = null,
        CancellationToken cancellationToken = default);

    Task<bool> ValidateRefreshTokenAsync(
        Guid userId,
        string refreshToken,
        CancellationToken cancellationToken = default);

    Task<AuthResponseDto?> RotateRefreshTokenAsync(
        Guid userId,
        string refreshToken,
        string email,
        string fullName,
        IEnumerable<string>? roles = null,
        CancellationToken cancellationToken = default);

    Task RevokeRefreshTokenAsync(
        Guid userId,
        string refreshToken,
        CancellationToken cancellationToken = default);

    Task RevokeAccessTokenAsync(
        string jti,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default);

    Task RevokeUserAccessTokensAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<bool> IsAccessTokenRevokedAsync(
        string jti,
        CancellationToken cancellationToken = default);

    Task<bool> IsAccessTokenRevokedAsync(
        string jti,
        Guid userId,
        DateTimeOffset issuedAt,
        CancellationToken cancellationToken = default);
}
