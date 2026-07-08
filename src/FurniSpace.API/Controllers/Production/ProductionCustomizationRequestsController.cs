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
public sealed class ProductionCustomizationRequestsController : BaseApiController
{
    private readonly ICustomizationRequestService _customizationRequests;

    public ProductionCustomizationRequestsController(ICustomizationRequestService customizationRequests)
    {
        _customizationRequests = customizationRequests;
    }

    [HttpGet("customization-requests")]
    public async Task<IActionResult> GetList(
        [FromQuery] ProductionCustomizationRequestQueryDto query,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _customizationRequests.GetProductionQueueAsync(
            currentUserId,
            query,
            cancellationToken);
        return ToActionResult(result);
    }

    private bool TryGetCurrentUserId(out Guid currentUserId)
    {
        return Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out currentUserId);
    }
}
