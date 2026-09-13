using System.Security.Cryptography;
using System.Text;

namespace FurniSpace.Infrastructure.Caching;

internal static class RedisKeyBuilder
{
    private const string Prefix = "furnispace";

    public static string RefreshToken(Guid userId, string refreshToken)
    {
        return $"{Prefix}:auth:refresh-token:{userId}:{Sha256(refreshToken)}";
    }

    public static string AccessTokenBlacklist(string jti)
    {
        return $"{Prefix}:auth:blacklist:{jti}";
    }

    public static string LoginAttempt(string email)
    {
        return $"{Prefix}:auth:login-attempt:{email.Trim().ToLowerInvariant()}";
    }

    public static string Otp(string email)
    {
        return $"{Prefix}:auth:otp:{email.Trim().ToLowerInvariant()}";
    }

    public static string PasswordReset(Guid userId, string tokenId)
    {
        return $"{Prefix}:auth:password-reset:{userId}:{tokenId}";
    }

    public static string Permissions(Guid userId)
    {
        return $"{Prefix}:auth:permissions:{userId}";
    }

    private static string Sha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
