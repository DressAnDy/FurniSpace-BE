#nullable enable

using System.Collections.Generic;
using FurniSpace.Application.Common.Payments;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FurniSpace.Application.Tests.Payments;

public sealed class SePayOptionsConfigurationTests
{
    [Fact]
    public void ApplyEnvironmentOverrides_AppliesConfiguredValues()
    {
        var options = new SePayOptions();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SEPAY_ENABLED"] = "true",
                ["SEPAY_ENVIRONMENT"] = "sandbox",
                ["SEPAY_MERCHANT_ID"] = "merchant",
                ["SEPAY_SECRET_KEY"] = "secret",
                ["SEPAY_WEBHOOK_URL"] = "https://example.com/sepay",
                ["SEPAY_WEBHOOK_AUTH_METHOD"] = "hmac",
                ["SEPAY_WEBHOOK_SECRET"] = "whsec",
                ["SEPAY_WEBHOOK_SIGNATURE_HEADER"] = "X-Signature",
                ["SEPAY_WEBHOOK_TIMESTAMP_HEADER"] = "X-Timestamp",
                ["SEPAY_WEBHOOK_TIMESTAMP_TOLERANCE_SECONDS"] = "600",
                ["SEPAY_BANK_CODE"] = "MB",
                ["SEPAY_BANK_ACCOUNT_NO"] = "1017588888",
                ["SEPAY_BANK_ACCOUNT_NAME"] = "FurniSpace",
                ["SEPAY_PAYMENT_CODE_PREFIX"] = "FS",
                ["SEPAY_PAYMENT_CODE_REGEX"] = @"FS[0-9]{8}",
                ["SEPAY_PAYMENT_CODE_RANDOM_DIGITS"] = "8",
                ["SEPAY_VIETQR_ENABLED"] = "true",
                ["SEPAY_VIETQR_BASE_URL"] = "https://qr.sepay.vn",
                ["SEPAY_VIETQR_TEMPLATE"] = "compact",
                ["SEPAY_VIETQR_SHOW_INFO"] = "true",
                ["SEPAY_VIETQR_FULL_ACCOUNT"] = "false",
                ["SEPAY_VIETQR_STORE_NAME"] = "FurniSpace",
                ["SEPAY_CURRENCY"] = "VND",
                ["SEPAY_STRICT_AMOUNT_CHECK"] = "true",
                ["SEPAY_ALLOW_PARTIAL_PAYMENT"] = "true",
                ["SEPAY_ALLOW_OVERPAYMENT"] = "false"
            })
            .Build();

        SePayOptionsConfiguration.ApplyEnvironmentOverrides(options, configuration);

        Assert.True(options.Enabled);
        Assert.Equal("sandbox", options.Environment);
        Assert.Equal("merchant", options.MerchantId);
        Assert.Equal("secret", options.SecretKey);
        Assert.Equal("https://example.com/sepay", options.WebhookUrl);
        Assert.Equal("hmac", options.WebhookAuthMethod);
        Assert.Equal("whsec", options.WebhookSecret);
        Assert.Equal("X-Signature", options.WebhookSignatureHeader);
        Assert.Equal("X-Timestamp", options.WebhookTimestampHeader);
        Assert.Equal(600, options.WebhookTimestampToleranceSeconds);
        Assert.Equal("MB", options.BankCode);
        Assert.Equal("1017588888", options.BankAccountNo);
        Assert.Equal("FurniSpace", options.BankAccountName);
        Assert.Equal("FS", options.PaymentCodePrefix);
        Assert.Equal(@"FS[0-9]{8}", options.PaymentCodeRegex);
        Assert.Equal(8, options.PaymentCodeRandomDigits);
        Assert.True(options.VietQrEnabled);
        Assert.Equal("https://qr.sepay.vn", options.VietQrBaseUrl);
        Assert.Equal("compact", options.VietQrTemplate);
        Assert.True(options.VietQrShowInfo);
        Assert.False(options.VietQrFullAccount);
        Assert.Equal("FurniSpace", options.VietQrStoreName);
        Assert.Equal("VND", options.Currency);
        Assert.True(options.StrictAmountCheck);
        Assert.True(options.AllowPartialPayment);
        Assert.False(options.AllowOverpayment);
    }
}
