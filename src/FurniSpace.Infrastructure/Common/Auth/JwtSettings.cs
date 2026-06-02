using System.Text;

namespace FurniSpace.Infrastructure.Common.Auth;

public sealed class JwtSettings
{
    public const string SectionName = "JwtSettings";
    public const int MinimumSecretKeyBytes = 32;

    public string SecretKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = "FurniSpace";
    public string Audience { get; set; } = "FurniSpace";
    public int AccessTokenExpirationMinutes { get; set; } = 15;
    public int RefreshTokenExpirationDays { get; set; } = 7;

    public byte[] GetSecretKeyBytes()
    {
        var keyBytes = TryDecodeBase64(SecretKey) ?? Encoding.UTF8.GetBytes(SecretKey);

        if (keyBytes.Length < MinimumSecretKeyBytes)
        {
            throw new InvalidOperationException(
                $"JWT secret must be at least {MinimumSecretKeyBytes} bytes after base64 decoding or UTF-8 conversion.");
        }

        return keyBytes;
    }

    private static byte[]? TryDecodeBase64(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            return Convert.FromBase64String(value);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
