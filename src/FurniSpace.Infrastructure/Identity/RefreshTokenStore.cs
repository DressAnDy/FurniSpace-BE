using FurniSpace.Application.Interfaces;

namespace FurniSpace.Infrastructure.Identity;

public sealed class RefreshTokenStore : IRefreshTokenStore
{
    private readonly ICacheService _cache;

    public RefreshTokenStore(ICacheService cache)
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

        var key = Caching.RedisKeyBuilder.RefreshToken(userId, refreshToken);
        return _cache.SetAsync(key, new RefreshTokenCacheEntry(userId, expiresAt), ttl, cancellationToken);
    }

    public Task<bool> ExistsAsync(Guid userId, string refreshToken, CancellationToken cancellationToken = default)
    {
        var key = Caching.RedisKeyBuilder.RefreshToken(userId, refreshToken);
        return _cache.ExistsAsync(key, cancellationToken);
    }

    public Task RevokeAsync(Guid userId, string refreshToken, CancellationToken cancellationToken = default)
    {
        var key = Caching.RedisKeyBuilder.RefreshToken(userId, refreshToken);
        return _cache.RemoveAsync(key, cancellationToken);
    }

    public Task RevokeAccessTokenAsync(string jti, DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
    {
        var ttl = expiresAt - DateTimeOffset.UtcNow;
        if (ttl <= TimeSpan.Zero)
        {
            return Task.CompletedTask;
        }

        var key = Caching.RedisKeyBuilder.AccessTokenBlacklist(jti);
        return _cache.SetAsync(key, true, ttl, cancellationToken);
    }

    public Task<bool> IsAccessTokenRevokedAsync(string jti, CancellationToken cancellationToken = default)
    {
        var key = Caching.RedisKeyBuilder.AccessTokenBlacklist(jti);
        return _cache.ExistsAsync(key, cancellationToken);
    }

    private sealed record RefreshTokenCacheEntry(Guid UserId, DateTimeOffset ExpiresAt);
}
