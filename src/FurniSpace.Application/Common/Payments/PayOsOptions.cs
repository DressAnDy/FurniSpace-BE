namespace FurniSpace.Application.Common.Payments;

public sealed class PayOsOptions
{
    public const string SectionName = "PayOS";

    public bool Enabled { get; set; }
    public string Environment { get; set; } = "production";
    public string ClientId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ChecksumKey { get; set; } = string.Empty;
    public string ApiBaseUrl { get; set; } = "https://api-merchant.payos.vn";
    public string ReturnUrl { get; set; } = string.Empty;
    public string CancelUrl { get; set; } = string.Empty;
    public string WebhookUrl { get; set; } = string.Empty;
    public string DescriptionPrefix { get; set; } = "FS";
    public int MaxDescriptionLength { get; set; } = 25;
}
