using Microsoft.Extensions.Configuration;

namespace FurniSpace.Application.Common.Payments;

internal static class SePayOptionsConfiguration
{
    public static void ApplyEnvironmentOverrides(SePayOptions options, IConfiguration configuration)
    {
        if (bool.TryParse(configuration["SEPAY_ENABLED"], out var enabled))
        {
            options.Enabled = enabled;
        }

        options.Environment = configuration["SEPAY_ENVIRONMENT"] ?? options.Environment;
        options.MerchantId = configuration["SEPAY_MERCHANT_ID"] ?? options.MerchantId;
        options.SecretKey = configuration["SEPAY_SECRET_KEY"] ?? options.SecretKey;
        options.WebhookUrl = configuration["SEPAY_WEBHOOK_URL"] ?? options.WebhookUrl;
        options.WebhookAuthMethod = configuration["SEPAY_WEBHOOK_AUTH_METHOD"] ?? options.WebhookAuthMethod;
        options.WebhookSecret = configuration["SEPAY_WEBHOOK_SECRET"] ?? options.WebhookSecret;
        options.WebhookSignatureHeader = configuration["SEPAY_WEBHOOK_SIGNATURE_HEADER"] ?? options.WebhookSignatureHeader;
        options.WebhookTimestampHeader = configuration["SEPAY_WEBHOOK_TIMESTAMP_HEADER"] ?? options.WebhookTimestampHeader;

        if (int.TryParse(configuration["SEPAY_WEBHOOK_TIMESTAMP_TOLERANCE_SECONDS"], out var tolerance))
        {
            options.WebhookTimestampToleranceSeconds = tolerance;
        }

        options.BankCode = configuration["SEPAY_BANK_CODE"] ?? options.BankCode;
        options.BankAccountNo = configuration["SEPAY_BANK_ACCOUNT_NO"] ?? options.BankAccountNo;
        options.BankAccountName = configuration["SEPAY_BANK_ACCOUNT_NAME"] ?? options.BankAccountName;
        options.PaymentCodePrefix = configuration["SEPAY_PAYMENT_CODE_PREFIX"] ?? options.PaymentCodePrefix;
        options.PaymentCodeRegex = configuration["SEPAY_PAYMENT_CODE_REGEX"] ?? options.PaymentCodeRegex;

        if (int.TryParse(configuration["SEPAY_PAYMENT_CODE_RANDOM_DIGITS"], out var randomDigits))
        {
            options.PaymentCodeRandomDigits = randomDigits;
        }

        if (bool.TryParse(configuration["SEPAY_VIETQR_ENABLED"], out var vietQrEnabled))
        {
            options.VietQrEnabled = vietQrEnabled;
        }

        options.VietQrBaseUrl = configuration["SEPAY_VIETQR_BASE_URL"] ?? options.VietQrBaseUrl;
        options.VietQrTemplate = configuration["SEPAY_VIETQR_TEMPLATE"] ?? options.VietQrTemplate;

        if (bool.TryParse(configuration["SEPAY_VIETQR_SHOW_INFO"], out var showInfo))
        {
            options.VietQrShowInfo = showInfo;
        }

        if (bool.TryParse(configuration["SEPAY_VIETQR_FULL_ACCOUNT"], out var fullAccount))
        {
            options.VietQrFullAccount = fullAccount;
        }

        options.VietQrStoreName = configuration["SEPAY_VIETQR_STORE_NAME"] ?? options.VietQrStoreName;
        options.Currency = configuration["SEPAY_CURRENCY"] ?? options.Currency;

        if (bool.TryParse(configuration["SEPAY_STRICT_AMOUNT_CHECK"], out var strictAmountCheck))
        {
            options.StrictAmountCheck = strictAmountCheck;
        }

        if (bool.TryParse(configuration["SEPAY_ALLOW_PARTIAL_PAYMENT"], out var allowPartialPayment))
        {
            options.AllowPartialPayment = allowPartialPayment;
        }

        if (bool.TryParse(configuration["SEPAY_ALLOW_OVERPAYMENT"], out var allowOverpayment))
        {
            options.AllowOverpayment = allowOverpayment;
        }
    }
}
