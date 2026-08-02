#nullable enable

using System.Security.Claims;
using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.CustomizationRequests;
using FurniSpace.Application.Interfaces.CustomizationRequests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers.Production;

[Authorize(Roles = "PRODUCTION,ADMIN")]
[Route("api/production")]
public sealed class ProductionCustomizationVersionsController : BaseApiController
{
    private readonly ICustomizationRequestService _customizationRequests;

    public ProductionCustomizationVersionsController(ICustomizationRequestService customizationRequests)
    {
        _customizationRequests = customizationRequests;
    }

    [HttpGet("customization-versions")]
    public async Task<IActionResult> GetQueue(
        [FromQuery] ProductionCustomizationVersionQueryDto query,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _customizationRequests.GetProductionVersionQueueAsync(
            currentUserId,
            query,
            cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("customization-versions/{customizationRequestVersionId:guid}")]
    public async Task<IActionResult> GetDetail(
        Guid customizationRequestVersionId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _customizationRequests.GetProductionVersionDetailAsync(
            customizationRequestVersionId,
            currentUserId,
            cancellationToken);
        return ToActionResult(result);
    }

    [HttpPatch("customization-versions/{customizationRequestVersionId:guid}/review")]
    public async Task<IActionResult> Review(
        Guid customizationRequestVersionId,
        [FromBody] ReviewCustomizationVersionDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _customizationRequests.ReviewVersionAsync(
            customizationRequestVersionId,
            currentUserId,
            request,
            cancellationToken);
        return ToActionResult(result);
    }

    private bool TryGetCurrentUserId(out Guid currentUserId)
    {
        return Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out currentUserId);
    }
}
