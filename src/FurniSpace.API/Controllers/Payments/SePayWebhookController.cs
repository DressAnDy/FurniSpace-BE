#nullable enable

using FurniSpace.Application.Common.Payments;
using FurniSpace.Application.Interfaces.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FurniSpace.API.Controllers.Payments;

[AllowAnonymous]
[ApiController]
[Route("api/webhooks")]
public sealed class SePayWebhookController : ControllerBase
{
    private readonly ISePayWebhookService _webhookService;
    private readonly SePayOptions _options;

    public SePayWebhookController(
        ISePayWebhookService webhookService,
        IOptions<SePayOptions> options)
    {
        _webhookService = webhookService;
        _options = options.Value;
    }

    [HttpPost("sepay")]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);
        var signature = Request.Headers[_options.WebhookSignatureHeader].FirstOrDefault();
        var timestampHeader = Request.Headers[_options.WebhookTimestampHeader].FirstOrDefault();

        var result = await _webhookService.ProcessAsync(rawBody, signature, timestampHeader, cancellationToken);
        if (result.Body is not null)
        {
            return StatusCode(result.StatusCode, result.Body);
        }

        return StatusCode(result.StatusCode, new { message = result.ErrorMessage });
    }
}
