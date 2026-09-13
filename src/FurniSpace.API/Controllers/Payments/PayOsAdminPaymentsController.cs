#nullable enable

using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.Payments;
using FurniSpace.Application.Interfaces.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers.Payments;

[Authorize(Roles = "ADMIN")]
[Route("api/admin/payments/payos")]
public sealed class PayOsAdminPaymentsController : BaseApiController
{
    private readonly IPaymentService _payments;

    public PayOsAdminPaymentsController(IPaymentService payments)
    {
        _payments = payments;
    }

    [HttpPost("confirm-webhook")]
    public async Task<IActionResult> ConfirmWebhook(
        [FromBody] PayOsConfirmWebhookRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var result = await _payments.ConfirmPayOsWebhookAsync(request, cancellationToken);
        return ToActionResult(result);
    }
}
