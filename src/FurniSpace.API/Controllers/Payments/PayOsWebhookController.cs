#nullable enable

using FurniSpace.Application.Interfaces.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers.Payments;

[AllowAnonymous]
[ApiController]
[Route("api/webhooks")]
public sealed class PayOsWebhookController : ControllerBase
{
    private readonly IPayOsWebhookService _webhookService;

    public PayOsWebhookController(IPayOsWebhookService webhookService)
    {
        _webhookService = webhookService;
    }

    [HttpPost("payos")]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);

        var result = await _webhookService.ProcessAsync(rawBody, cancellationToken);
        if (result.Body is not null)
        {
            return StatusCode(result.StatusCode, result.Body);
        }

        return StatusCode(result.StatusCode, new { message = result.ErrorMessage });
    }
}
