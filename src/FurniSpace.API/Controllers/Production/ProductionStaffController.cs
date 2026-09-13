#nullable enable

using System.Security.Claims;
using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.Production;
using FurniSpace.Application.Interfaces.Production;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers.Production;

[Authorize(Roles = "SALES,ADMIN")]
[Route("production-staff")]
public sealed class ProductionStaffController : BaseApiController
{
    private readonly IProductionRequestService _productionRequests;

    public ProductionStaffController(IProductionRequestService productionRequests)
    {
        _productionRequests = productionRequests;
    }

    [HttpGet("available")]
    public async Task<IActionResult> GetAvailable(
        [FromQuery] AvailableProductionStaffQueryDto query,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _productionRequests.GetAvailableStaffAsync(
            currentUserId,
            query,
            cancellationToken);
        return ToActionResult(result);
    }
}
