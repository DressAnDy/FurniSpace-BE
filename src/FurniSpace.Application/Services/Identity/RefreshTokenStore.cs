using System.Security.Cryptography;
using System.Text;
using FurniSpace.Application.Interfaces.Identity;
using InfrastructureCacheService = FurniSpace.Infrastructure.Interfaces.ICacheService;

namespace FurniSpace.Application.Services.Identity;

public sealed class RefreshTokenStore : IRefreshTokenStore
{
    private const string Prefix = "furnispace";

    private readonly InfrastructureCacheService _cache;

    public RefreshTokenStore(InfrastructureCacheService cache)
    {
        _cache = cache;
    }

    public Task StoreAsync(Guid userId, string refreshToken, DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
    {
        var ttl = expiresAt - DateTimeOffset.UtcNow;
        if (ttl <= TimeSpan.Zero)
        {
            return Task.CompletedTask;
        }

        var key = RefreshTokenKey(userId, refreshToken);
        return _cache.SetAsync(key, new RefreshTokenCacheEntry(userId, expiresAt), ttl, cancellationToken);
    }

    public Task<bool> ExistsAsync(Guid userId, string refreshToken, CancellationToken cancellationToken = default)
    {
        var key = RefreshTokenKey(userId, refreshToken);
        return _cache.ExistsAsync(key, cancellationToken);
    }

    public Task RevokeAsync(Guid userId, string refreshToken, CancellationToken cancellationToken = default)
    {
        var key = RefreshTokenKey(userId, refreshToken);
        return _cache.RemoveAsync(key, cancellationToken);
    }

    public Task RevokeAccessTokenAsync(string jti, DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
    {
        var ttl = expiresAt - DateTimeOffset.UtcNow;
        if (ttl <= TimeSpan.Zero)
        {
            return Task.CompletedTask;
        }

        var key = AccessTokenBlacklistKey(jti);
        return _cache.SetAsync(key, true, ttl, cancellationToken);
    }

    public Task<bool> IsAccessTokenRevokedAsync(string jti, CancellationToken cancellationToken = default)
    {
        var key = AccessTokenBlacklistKey(jti);
        return _cache.ExistsAsync(key, cancellationToken);
    }

    private static string RefreshTokenKey(Guid userId, string refreshToken)
    {
        return $"{Prefix}:auth:refresh-token:{userId}:{Sha256(refreshToken)}";
    }

    private static string AccessTokenBlacklistKey(string jti)
    {
        return $"{Prefix}:auth:blacklist:{jti}";
    }

    private static string Sha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private sealed record RefreshTokenCacheEntry(Guid UserId, DateTimeOffset ExpiresAt);
}
