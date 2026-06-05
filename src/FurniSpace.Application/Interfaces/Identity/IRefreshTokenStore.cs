namespace FurniSpace.Application.Interfaces.Identity;

public interface IRefreshTokenStore
{
    Task StoreAsync(Guid userId, string refreshToken, DateTimeOffset expiresAt, CancellationToken cancellationToken = default);
    Task<Guid?> ResolveUserIdAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<bool> ConsumeAsync(Guid userId, string refreshToken, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid userId, string refreshToken, CancellationToken cancellationToken = default);
    Task RevokeAsync(Guid userId, string refreshToken, CancellationToken cancellationToken = default);
    Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken = default);
    Task RevokeAccessTokenAsync(string jti, DateTimeOffset expiresAt, CancellationToken cancellationToken = default);
    Task RevokeUserAccessTokensAsync(Guid userId, DateTimeOffset revokedAt, TimeSpan ttl, CancellationToken cancellationToken = default);
    Task<bool> IsAccessTokenRevokedAsync(string jti, CancellationToken cancellationToken = default);
    Task<bool> AreUserAccessTokensRevokedAsync(Guid userId, DateTimeOffset issuedAt, CancellationToken cancellationToken = default);
}
