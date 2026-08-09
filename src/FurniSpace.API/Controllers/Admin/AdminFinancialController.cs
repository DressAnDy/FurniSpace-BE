#nullable enable

using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.Financial;
using FurniSpace.Application.Interfaces.Financial;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers.Admin;

[Route("admin/financial")]
public sealed class AdminFinancialController : BaseApiController
{
    private readonly IAdminFinancialService _financial;

    public AdminFinancialController(IAdminFinancialService financial)
    {
        _financial = financial;
    }

    [Authorize(Roles = "ADMIN")]
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] AdminFinancialSummaryQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var result = await _financial.GetSummaryAsync(query, cancellationToken);
        return ToActionResult(result);
    }
}
