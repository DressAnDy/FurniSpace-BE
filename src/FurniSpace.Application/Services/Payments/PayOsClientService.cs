using System.Text.Json;
using FurniSpace.Application.Common.Payments;
using FurniSpace.Application.Interfaces.Payments;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PayOS;
using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;

namespace FurniSpace.Application.Services.Payments;

public sealed class PayOsClientService : IPayOsClient
{
    private static readonly JsonSerializerOptions WebhookSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly PayOSClient _client;
    private readonly ILogger<PayOsClientService>? _logger;

    public PayOsClientService(IOptions<PayOsOptions> options, ILogger<PayOsClientService>? logger = null)
    {
        var payOsOptions = options.Value;
        _client = new PayOSClient(new PayOSOptions
        {
            ClientId = payOsOptions.ClientId,
            ApiKey = payOsOptions.ApiKey,
            ChecksumKey = payOsOptions.ChecksumKey,
            BaseUrl = payOsOptions.ApiBaseUrl
        });
        _logger = logger;
    }

    public async Task<PayOsCreatePaymentLinkResult> CreatePaymentLinkAsync(
        PayOsCreatePaymentLinkRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        try
        {
            var paymentRequest = new CreatePaymentLinkRequest
            {
                OrderCode = request.OrderCode,
                Amount = request.Amount,
                Description = request.Description,
                ReturnUrl = request.ReturnUrl,
                CancelUrl = request.CancelUrl
            };

            var response = await _client.PaymentRequests.CreateAsync(paymentRequest);
            return new PayOsCreatePaymentLinkResult
            {
                CheckoutUrl = response.CheckoutUrl ?? string.Empty,
                QrCode = response.QrCode,
                PaymentLinkId = response.PaymentLinkId
            };
        }
        catch (Exception exception)
        {
            _logger?.LogError(
                exception,
                "PayOS create payment link failed. OrderCode={OrderCode}, Amount={Amount}",
                request.OrderCode,
                request.Amount);
            throw;
        }
    }

    public async Task<PayOsVerifiedWebhookData> VerifyWebhookAsync(
        string rawBody,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        var webhook = JsonSerializer.Deserialize<Webhook>(rawBody, WebhookSerializerOptions)
            ?? throw new InvalidOperationException("Invalid PayOS webhook payload.");

        var verified = await _client.Webhooks.VerifyAsync(webhook);
        return new PayOsVerifiedWebhookData
        {
            OrderCode = verified.OrderCode,
            Amount = verified.Amount,
            Reference = verified.Reference,
            PaymentLinkId = verified.PaymentLinkId,
            TransactionDateTime = verified.TransactionDateTime,
            Code = verified.Code
        };
    }

    public async Task<string> ConfirmWebhookAsync(
        string webhookUrl,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        var result = await _client.Webhooks.ConfirmAsync(webhookUrl);
        return result.WebhookUrl ?? webhookUrl;
    }
}
