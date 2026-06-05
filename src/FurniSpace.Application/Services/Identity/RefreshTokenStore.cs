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

        return StoreTokenKeysAsync(userId, refreshToken, expiresAt, ttl, cancellationToken);
    }

    public Task<Guid?> ResolveUserIdAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        return _cache.GetAsync<Guid?>(RefreshTokenLookupKey(refreshToken), cancellationToken);
    }

    public Task<bool> ExistsAsync(Guid userId, string refreshToken, CancellationToken cancellationToken = default)
    {
        var key = RefreshTokenKey(userId, refreshToken);
        return _cache.ExistsAsync(key, cancellationToken);
    }

    public async Task<bool> ConsumeAsync(Guid userId, string refreshToken, CancellationToken cancellationToken = default)
    {
        var key = RefreshTokenKey(userId, refreshToken);
        var entry = await _cache.GetAndRemoveAsync<RefreshTokenCacheEntry>(key, cancellationToken);
        if (entry is null)
        {
            return false;
        }

        await _cache.RemoveAsync(RefreshTokenLookupKey(refreshToken), cancellationToken);
        return true;
    }

    public async Task RevokeAsync(Guid userId, string refreshToken, CancellationToken cancellationToken = default)
    {
        await _cache.RemoveAsync(RefreshTokenKey(userId, refreshToken), cancellationToken);
        await _cache.RemoveAsync(RefreshTokenLookupKey(refreshToken), cancellationToken);
    }

    public Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _cache.RemoveByPrefixAsync($"{Prefix}:auth:refresh-token:{userId}:", cancellationToken);
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

    public Task RevokeUserAccessTokensAsync(
        Guid userId,
        DateTimeOffset revokedAt,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        if (ttl <= TimeSpan.Zero)
        {
            return Task.CompletedTask;
        }

        return _cache.SetAsync(UserAccessRevokedBeforeKey(userId), revokedAt.ToUnixTimeSeconds(), ttl, cancellationToken);
    }

    public Task<bool> IsAccessTokenRevokedAsync(string jti, CancellationToken cancellationToken = default)
    {
        var key = AccessTokenBlacklistKey(jti);
        return _cache.ExistsAsync(key, cancellationToken);
    }

    public async Task<bool> AreUserAccessTokensRevokedAsync(
        Guid userId,
        DateTimeOffset issuedAt,
        CancellationToken cancellationToken = default)
    {
        var revokedBefore = await _cache.GetAsync<long?>(UserAccessRevokedBeforeKey(userId), cancellationToken);
        return revokedBefore.HasValue && issuedAt.ToUnixTimeSeconds() <= revokedBefore.Value;
    }

    private static string RefreshTokenKey(Guid userId, string refreshToken)
    {
        return $"{Prefix}:auth:refresh-token:{userId}:{Sha256(refreshToken)}";
    }

    private static string RefreshTokenLookupKey(string refreshToken)
    {
        return $"{Prefix}:auth:refresh-token-lookup:{Sha256(refreshToken)}";
    }

    private static string AccessTokenBlacklistKey(string jti)
    {
        return $"{Prefix}:auth:blacklist:{jti}";
    }

    private static string UserAccessRevokedBeforeKey(Guid userId)
    {
        return $"{Prefix}:auth:access-revoked-before:{userId}";
    }

    private static string Sha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private async Task StoreTokenKeysAsync(
        Guid userId,
        string refreshToken,
        DateTimeOffset expiresAt,
        TimeSpan ttl,
        CancellationToken cancellationToken)
    {
        await _cache.SetAsync(
            RefreshTokenKey(userId, refreshToken),
            new RefreshTokenCacheEntry(userId, expiresAt),
            ttl,
            cancellationToken);
        await _cache.SetAsync(RefreshTokenLookupKey(refreshToken), userId, ttl, cancellationToken);
    }

    private sealed record RefreshTokenCacheEntry(Guid UserId, DateTimeOffset ExpiresAt);
}
