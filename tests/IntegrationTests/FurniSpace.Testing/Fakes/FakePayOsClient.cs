using FurniSpace.Application.Interfaces.Payments;

namespace FurniSpace.Testing.Fakes;

public sealed class FakePayOsClient : IPayOsClient
{
    public PayOsVerifiedWebhookData VerifiedWebhook { get; set; } = new();

    public Task<PayOsCreatePaymentLinkResult> CreatePaymentLinkAsync(
        PayOsCreatePaymentLinkRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new PayOsCreatePaymentLinkResult
        {
            CheckoutUrl = $"https://payos.integration.test/{request.OrderCode}",
            PaymentLinkId = $"integration-{request.OrderCode}"
        });

    public Task<PayOsVerifiedWebhookData> VerifyWebhookAsync(
        string rawBody,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(VerifiedWebhook);

    public Task<string> ConfirmWebhookAsync(
        string webhookUrl,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(webhookUrl);
}
