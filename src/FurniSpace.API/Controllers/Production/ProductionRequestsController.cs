#nullable enable

using System.Security.Claims;
using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.Production;
using FurniSpace.Application.Interfaces.Production;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers.Production;

[Authorize(Roles = "PRODUCTION,SALES,ADMIN")]
[Route("production-requests")]
public sealed class ProductionRequestsController : BaseApiController
{
    private readonly IProductionRequestService _productionRequests;

    public ProductionRequestsController(IProductionRequestService productionRequests)
    {
        _productionRequests = productionRequests;
    }

    [HttpGet]
    public async Task<IActionResult> GetQueue(
        [FromQuery] ProductionRequestQueryDto query,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _productionRequests.GetQueueAsync(
            currentUserId,
            query,
            cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("{productionRequestId:guid}")]
    public async Task<IActionResult> GetDetail(
        Guid productionRequestId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _productionRequests.GetDetailAsync(
            productionRequestId,
            currentUserId,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "SALES,ADMIN")]
    [HttpPatch("{productionRequestId:guid}/assign")]
    public async Task<IActionResult> Assign(
        Guid productionRequestId,
        [FromBody] AssignProductionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _productionRequests.AssignAsync(
            productionRequestId,
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
