#nullable enable

using System;
using System.Security.Cryptography;
using System.Text;
using FurniSpace.Application.Common.Payments;
using Microsoft.Extensions.Options;
using Xunit;

namespace FurniSpace.Application.Tests.Payments;

public sealed class SePayWebhookSignatureVerifierTests
{
    [Fact]
    public void Verify_WithValidSignature_ReturnsValidResult()
    {
        const string secret = "whsec_test_secret";
        const string rawBody = "{\"id\":1}";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signature = ComputeSignature(secret, timestamp, rawBody);
        var verifier = CreateVerifier(secret);

        var result = verifier.Verify(rawBody, signature, timestamp);

        Assert.True(result.IsValid);
        Assert.Equal(signature, result.Signature);
        Assert.Equal(timestamp, result.TimestampHeader);
    }

    [Fact]
    public void Verify_WithInvalidSignature_ReturnsInvalidResult()
    {
        var verifier = CreateVerifier("whsec_test_secret");
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        var result = verifier.Verify("{\"id\":1}", "sha256=invalid", timestamp);

        Assert.False(result.IsValid);
        Assert.Equal("Webhook signature is invalid.", result.ErrorMessage);
    }

    [Fact]
    public void Verify_WithExpiredTimestamp_ReturnsInvalidResult()
    {
        const string secret = "whsec_test_secret";
        const string rawBody = "{\"id\":1}";
        var timestamp = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds().ToString();
        var signature = ComputeSignature(secret, timestamp, rawBody);
        var verifier = CreateVerifier(secret, toleranceSeconds: 60);

        var result = verifier.Verify(rawBody, signature, timestamp);

        Assert.False(result.IsValid);
        Assert.Equal("Webhook timestamp is outside the allowed tolerance.", result.ErrorMessage);
    }

    private static SePayWebhookSignatureVerifier CreateVerifier(string secret, int toleranceSeconds = 300)
    {
        var options = Options.Create(new SePayOptions
        {
            WebhookSecret = secret,
            WebhookTimestampToleranceSeconds = toleranceSeconds
        });
        return new SePayWebhookSignatureVerifier(options);
    }

    private static string ComputeSignature(string secret, string timestamp, string rawBody)
    {
        var message = $"{timestamp}.{rawBody}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}
