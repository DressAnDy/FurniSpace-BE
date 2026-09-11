#nullable enable

using System.Security.Claims;
using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.Production;
using FurniSpace.Application.Interfaces.Production;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers.Production;

[Authorize(Roles = "PRODUCTION,ADMIN")]
[Route("production-items")]
public sealed class ProductionItemsController : BaseApiController
{
    private readonly IProductionRequestService _productionRequests;

    public ProductionItemsController(IProductionRequestService productionRequests)
    {
        _productionRequests = productionRequests;
    }

    [HttpPatch("{productionItemId:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid productionItemId,
        [FromBody] UpdateProductionItemStatusDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _productionRequests.UpdateItemStatusAsync(
            productionItemId,
            currentUserId,
            request,
            cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("unavailable")]
    public async Task<IActionResult> GetUnavailableItems(
        [FromQuery] ProductionUnavailableItemsQueryDto query,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _productionRequests.GetUnavailableItemsAsync(currentUserId, query, cancellationToken);
        return ToActionResult(result);
    }

    private bool TryGetCurrentUserId(out Guid currentUserId)
    {
        return Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out currentUserId);
    }
}
