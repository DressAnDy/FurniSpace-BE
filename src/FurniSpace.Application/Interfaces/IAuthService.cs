namespace FurniSpace.Application.Interfaces;

using FurniSpace.Application.DTOs;

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

    Task<bool> IsAccessTokenRevokedAsync(
        string jti,
        CancellationToken cancellationToken = default);
}
