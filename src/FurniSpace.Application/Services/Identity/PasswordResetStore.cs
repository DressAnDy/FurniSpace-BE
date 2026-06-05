using System.Security.Cryptography;
using System.Text;
using FurniSpace.Application.Interfaces.Identity;
using InfrastructureCacheService = FurniSpace.Infrastructure.Interfaces.ICacheService;

namespace FurniSpace.Application.Services.Identity;

public sealed class PasswordResetStore : IPasswordResetStore
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(30);
    private readonly InfrastructureCacheService _cache;

    public PasswordResetStore(InfrastructureCacheService cache)
    {
        _cache = cache;
    }

    public async Task<string> CreateAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        await _cache.SetAsync(Key(userId, token), true, TokenLifetime, cancellationToken);
        return token;
    }

    public async Task<bool> ConsumeAsync(Guid userId, string token, CancellationToken cancellationToken = default)
    {
        var key = Key(userId, token);
        return await _cache.GetAndRemoveAsync<bool?>(key, cancellationToken) == true;
    }

    private static string Key(Guid userId, string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return $"furnispace:auth:password-reset:{userId}:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}
