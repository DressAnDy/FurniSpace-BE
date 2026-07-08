namespace FurniSpace.Application.Interfaces.Payments;

public interface IPayOsClient
{
    Task<PayOsCreatePaymentLinkResult> CreatePaymentLinkAsync(
        PayOsCreatePaymentLinkRequest request,
        CancellationToken cancellationToken = default);

    Task<PayOsVerifiedWebhookData> VerifyWebhookAsync(
        string rawBody,
        CancellationToken cancellationToken = default);

    Task<string> ConfirmWebhookAsync(
        string webhookUrl,
        CancellationToken cancellationToken = default);
}

public sealed class PayOsCreatePaymentLinkRequest
{
    public long OrderCode { get; set; }
    public int Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string ReturnUrl { get; set; } = string.Empty;
    public string CancelUrl { get; set; } = string.Empty;
}

public sealed class PayOsCreatePaymentLinkResult
{
    public string CheckoutUrl { get; set; } = string.Empty;
    public string? QrCode { get; set; }
    public string? PaymentLinkId { get; set; }
}

public sealed class PayOsVerifiedWebhookData
{
    public long OrderCode { get; set; }
    public long Amount { get; set; }
    public string? Reference { get; set; }
    public string? PaymentLinkId { get; set; }
    public string? TransactionDateTime { get; set; }
    public string? Code { get; set; }
}
