using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace FurniSpace.Application.Common.Payments;

public sealed class SePayWebhookSignatureVerifier
{
    private readonly SePayOptions _options;

    public SePayWebhookSignatureVerifier(IOptions<SePayOptions> options)
    {
        _options = options.Value;
    }

    public SePayWebhookVerificationResult Verify(string rawBody, string? signature, string? timestampHeader)
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookSecret))
        {
            return SePayWebhookVerificationResult.Invalid("Webhook secret is not configured.");
        }

        if (string.IsNullOrWhiteSpace(signature) || string.IsNullOrWhiteSpace(timestampHeader))
        {
            return SePayWebhookVerificationResult.Invalid("Missing webhook signature headers.");
        }

        if (!long.TryParse(timestampHeader, out var timestamp))
        {
            return SePayWebhookVerificationResult.Invalid("Invalid webhook timestamp.");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (Math.Abs(now - timestamp) > _options.WebhookTimestampToleranceSeconds)
        {
            return SePayWebhookVerificationResult.Invalid("Webhook timestamp is outside the allowed tolerance.");
        }

        var message = $"{timestamp}.{rawBody}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.WebhookSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        var expected = "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(signature))
            ? SePayWebhookVerificationResult.Valid(signature, timestampHeader)
            : SePayWebhookVerificationResult.Invalid("Webhook signature is invalid.");
    }
}

public sealed record SePayWebhookVerificationResult(
    bool IsValid,
    string? Signature,
    string? TimestampHeader,
    string? ErrorMessage)
{
    public static SePayWebhookVerificationResult Valid(string signature, string timestampHeader)
    {
        return new SePayWebhookVerificationResult(true, signature, timestampHeader, null);
    }

    public static SePayWebhookVerificationResult Invalid(string errorMessage)
    {
        return new SePayWebhookVerificationResult(false, null, null, errorMessage);
    }
}
