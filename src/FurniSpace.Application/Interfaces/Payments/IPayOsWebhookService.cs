using FurniSpace.Application.DTOs.Payments;

namespace FurniSpace.Application.Interfaces.Payments;

public interface IPayOsWebhookService
{
    Task<PayOsWebhookProcessResult> ProcessAsync(
        string rawBody,
        CancellationToken cancellationToken = default);
}

public sealed record PayOsWebhookProcessResult(
    int StatusCode,
    PayOsWebhookSuccessDto? Body,
    string? ErrorMessage);
