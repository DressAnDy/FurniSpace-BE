#nullable enable

using System.Security.Claims;
using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.Payments;
using FurniSpace.Application.Interfaces.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers.Payments;

[Authorize(Roles = "ADMIN")]
[Route("api/test")]
public sealed class TestPaymentsController : BaseApiController
{
    private readonly IPaymentService _payments;

    public TestPaymentsController(IPaymentService payments)
    {
        _payments = payments;
    }

    [HttpPost("payments")]
    public async Task<IActionResult> CreateTestPayment(
        [FromBody] CreateTestPaymentRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _payments.CreateTestPaymentAsync(currentUserId, request, cancellationToken);
        return ToActionResult(result);
    }

    private bool TryGetCurrentUserId(out Guid currentUserId)
    {
        return Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out currentUserId);
    }
}
