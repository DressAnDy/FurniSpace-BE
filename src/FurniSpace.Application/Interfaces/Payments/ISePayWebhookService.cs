using FurniSpace.Application.DTOs.Payments;

namespace FurniSpace.Application.Interfaces.Payments;

public interface ISePayWebhookService
{
    Task<SePayWebhookProcessResult> ProcessAsync(
        string rawBody,
        string? signature,
        string? timestampHeader,
        CancellationToken cancellationToken = default);
}

public sealed record SePayWebhookProcessResult(int StatusCode, SePayWebhookSuccessDto? Body, string? ErrorMessage);
