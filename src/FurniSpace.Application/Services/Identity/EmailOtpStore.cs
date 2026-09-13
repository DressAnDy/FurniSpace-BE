using System.Security.Cryptography;
using System.Text;
using FurniSpace.Application.Common.Auth;
using FurniSpace.Application.Interfaces.Identity;
using Microsoft.Extensions.Options;
using InfrastructureCacheService = FurniSpace.Infrastructure.Interfaces.ICacheService;

namespace FurniSpace.Application.Services.Identity;

public sealed class EmailOtpStore : IEmailOtpStore
{
    private static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(5);
    private readonly InfrastructureCacheService _cache;
    private readonly byte[] _pepper;

    public EmailOtpStore(InfrastructureCacheService cache, IOptions<JwtSettings> jwtSettings)
    {
        _cache = cache;
        _pepper = jwtSettings.Value.GetSecretKeyBytes();
    }

    public async Task<string> CreateAsync(string email, CancellationToken cancellationToken = default)
    {
        var otpCode = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        await _cache.SetAsync(Key(email), HashOtp(email, otpCode), OtpLifetime, cancellationToken);
        return otpCode;
    }

    public async Task<bool> ConsumeAsync(string email, string otpCode, CancellationToken cancellationToken = default)
    {
        var key = Key(email);
        return await _cache.CompareAndRemoveAsync(key, HashOtp(email, otpCode), cancellationToken);
    }

    private string HashOtp(string email, string otpCode)
    {
        var normalized = $"{NormalizeEmail(email)}:{otpCode.Trim()}";
        using var hmac = new HMACSHA256(_pepper);
        var bytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string Key(string email)
    {
        return $"furnispace:auth:otp:{Sha256(NormalizeEmail(email))}";
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static string Sha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
