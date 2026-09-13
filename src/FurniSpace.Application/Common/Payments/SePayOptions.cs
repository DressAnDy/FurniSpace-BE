namespace FurniSpace.Application.Common.Payments;

public sealed class SePayOptions
{
    public const string SectionName = "SePay";

    public bool Enabled { get; set; }
    public string Environment { get; set; } = "production";
    public string MerchantId { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string WebhookUrl { get; set; } = string.Empty;
    public string WebhookAuthMethod { get; set; } = "HMAC_SHA256";
    public string WebhookSecret { get; set; } = string.Empty;
    public string WebhookSignatureHeader { get; set; } = "X-SePay-Signature";
    public string WebhookTimestampHeader { get; set; } = "X-SePay-Timestamp";
    public int WebhookTimestampToleranceSeconds { get; set; } = 300;
    public string BankCode { get; set; } = string.Empty;
    public string BankAccountNo { get; set; } = string.Empty;
    public string BankAccountName { get; set; } = string.Empty;
    public string PaymentCodePrefix { get; set; } = "FS";
    public string PaymentCodeRegex { get; set; } = @"FS[0-9]{8,10}";
    public int PaymentCodeRandomDigits { get; set; } = 8;
    public bool VietQrEnabled { get; set; } = true;
    public string VietQrBaseUrl { get; set; } = "https://vietqr.app/img";
    public string VietQrTemplate { get; set; } = "compact";
    public bool VietQrShowInfo { get; set; } = true;
    public bool VietQrFullAccount { get; set; }
    public string VietQrStoreName { get; set; } = "FURNISPACE";
    public string Currency { get; set; } = "VND";
    public bool StrictAmountCheck { get; set; } = true;
    public bool AllowPartialPayment { get; set; } = true;
    public bool AllowOverpayment { get; set; }
}
