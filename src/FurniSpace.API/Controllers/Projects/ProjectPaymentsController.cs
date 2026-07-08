#nullable enable

using System.Security.Claims;
using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.Payments;
using FurniSpace.Application.Interfaces.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers.Projects;

[Authorize]
[Route("api/projects")]
public sealed class ProjectPaymentsController : BaseApiController
{
    private readonly IPaymentService _payments;

    public ProjectPaymentsController(IPaymentService payments)
    {
        _payments = payments;
    }

    [Authorize(Roles = "SALES,ADMIN")]
    [HttpPost("{projectId:guid}/payments/project-start-fee")]
    public async Task<IActionResult> CreateProjectStartFeePayment(
        Guid projectId,
        [FromBody] CreateProjectStartFeePaymentRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _payments.CreateProjectStartFeePaymentAsync(
            projectId,
            currentUserId,
            request,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "SALES,ADMIN")]
    [HttpGet("{projectId:guid}/payments/project-start-fee/status")]
    public async Task<IActionResult> GetProjectStartFeeStatus(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _payments.GetProjectStartFeeStatusAsync(
            projectId,
            currentUserId,
            cancellationToken);
        return ToActionResult(result);
    }

    private bool TryGetCurrentUserId(out Guid currentUserId)
    {
        return Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out currentUserId);
    }
}
