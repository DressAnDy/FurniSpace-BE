#nullable enable

using System.Collections.Generic;
using FurniSpace.Application.Common.Payments;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FurniSpace.Application.Tests.Payments;

public sealed class PayOsOptionsConfigurationTests
{
    [Fact]
    public void ApplyEnvironmentOverrides_AppliesConfiguredValues()
    {
        var options = new PayOsOptions();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PAYOS_ENABLED"] = "true",
                ["PAYOS_ENVIRONMENT"] = "sandbox",
                ["PAYOS_CLIENT_ID"] = "client-id",
                ["PAYOS_API_KEY"] = "api-key",
                ["PAYOS_CHECKSUM_KEY"] = "checksum",
                ["PAYOS_API_BASE_URL"] = "https://api-merchant.payos.vn",
                ["PAYOS_RETURN_URL"] = "https://example.com/return",
                ["PAYOS_CANCEL_URL"] = "https://example.com/cancel",
                ["PAYOS_WEBHOOK_URL"] = "https://example.com/webhook",
                ["PAYOS_DESCRIPTION_PREFIX"] = "FS ",
                ["PAYOS_MAX_DESCRIPTION_LENGTH"] = "25"
            })
            .Build();

        PayOsOptionsConfiguration.ApplyEnvironmentOverrides(options, configuration);

        Assert.True(options.Enabled);
        Assert.Equal("sandbox", options.Environment);
        Assert.Equal("client-id", options.ClientId);
        Assert.Equal("api-key", options.ApiKey);
        Assert.Equal("checksum", options.ChecksumKey);
        Assert.Equal("https://api-merchant.payos.vn", options.ApiBaseUrl);
        Assert.Equal("https://example.com/return", options.ReturnUrl);
        Assert.Equal("https://example.com/cancel", options.CancelUrl);
        Assert.Equal("https://example.com/webhook", options.WebhookUrl);
        Assert.Equal("FS ", options.DescriptionPrefix);
        Assert.Equal(25, options.MaxDescriptionLength);
    }
}
