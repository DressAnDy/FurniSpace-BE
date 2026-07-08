#nullable enable

using System;
using System.Text.Json;
using System.Threading.Tasks;
using FurniSpace.Application.Common.Payments;
using FurniSpace.Application.Interfaces.Payments;
using FurniSpace.Application.Services.Payments;
using Microsoft.Extensions.Options;
using Xunit;

namespace FurniSpace.Application.Tests.Payments;

public sealed class PayOsClientServiceTests
{
    private static PayOsOptions CreateOptions() => new()
    {
        ClientId = "test-client",
        ApiKey = "test-api-key",
        ChecksumKey = "test-checksum",
        ApiBaseUrl = "https://api-merchant.payos.vn"
    };

    [Fact]
    public void Constructor_WithOptions_CreatesService()
    {
        var service = new PayOsClientService(Options.Create(CreateOptions()));

        Assert.NotNull(service);
    }

    [Fact]
    public async Task VerifyWebhookAsync_WithInvalidJson_ThrowsJsonException()
    {
        var service = new PayOsClientService(Options.Create(CreateOptions()));

        await Assert.ThrowsAsync<JsonException>(() => service.VerifyWebhookAsync("not-json"));
    }

    [Fact]
    public async Task CreatePaymentLinkAsync_WhenApiFails_ThrowsInvalidOperationException()
    {
        var service = new PayOsClientService(Options.Create(new PayOsOptions
        {
            ClientId = "test-client",
            ApiKey = "test-api-key",
            ChecksumKey = "test-checksum",
            ApiBaseUrl = "http://127.0.0.1:1"
        }));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreatePaymentLinkAsync(
                new PayOsCreatePaymentLinkRequest
                {
                    OrderCode = 202607080001L,
                    Amount = 10000,
                    Description = "FS12345678",
                    ReturnUrl = "https://example.com/return",
                    CancelUrl = "https://example.com/cancel"
                }));

        Assert.Contains("PayOS create payment link failed", exception.Message);
    }
}
