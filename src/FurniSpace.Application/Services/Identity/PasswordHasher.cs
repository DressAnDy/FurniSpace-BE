using System.Security.Cryptography;
using FurniSpace.Application.Common.Auth;
using FurniSpace.Application.Interfaces.Identity;
using Microsoft.Extensions.Options;

namespace FurniSpace.Application.Services.Identity;

public sealed class PasswordHasher : IPasswordHasher
{
    private readonly PasswordHashingSettings _settings;

    public PasswordHasher(IOptions<PasswordHashingSettings> settings)
    {
        _settings = settings.Value;
        _settings.Validate();
    }

    public string Hash(string password)
    {
        var algorithm = _settings.GetAlgorithm();
        var salt = RandomNumberGenerator.GetBytes(_settings.SaltSizeBytes);
        var key = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            _settings.Iterations,
            algorithm,
            _settings.KeySizeBytes);

        return $"pbkdf2-{algorithm.Name!.ToLowerInvariant()}${_settings.Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }

    public bool Verify(string password, string passwordHash)
    {
        var parts = passwordHash.Split('$');
        if (parts.Length != 4 ||
            !TryGetAlgorithm(parts[0], out var algorithm) ||
            !int.TryParse(parts[1], out var iterations) ||
            iterations < 1)
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, algorithm, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (Exception exception) when (exception is FormatException or CryptographicException or ArgumentException)
        {
            return false;
        }
    }

    private static bool TryGetAlgorithm(string marker, out HashAlgorithmName algorithm)
    {
        algorithm = marker.ToLowerInvariant() switch
        {
            "pbkdf2-sha256" => HashAlgorithmName.SHA256,
            "pbkdf2-sha384" => HashAlgorithmName.SHA384,
            "pbkdf2-sha512" => HashAlgorithmName.SHA512,
            _ => default
        };

        return algorithm != default;
    }
}
