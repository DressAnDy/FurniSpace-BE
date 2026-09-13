using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.BusinessTypes;
using FurniSpace.Application.Interfaces.BusinessTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers.Catalog;

[Route("business-types")]
public sealed class BusinessTypesController : BaseApiController
{
    private readonly IBusinessTypeService _businessTypes;

    public BusinessTypesController(IBusinessTypeService businessTypes)
    {
        _businessTypes = businessTypes;
    }

    [HttpPost]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Create(
        [FromBody] CreateBusinessTypeRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var result = await _businessTypes.CreateAsync(request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPatch("{businessTypeId:int}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Update(
        int businessTypeId,
        [FromBody] UpdateBusinessTypeRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var result = await _businessTypes.UpdateAsync(businessTypeId, request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPatch("{businessTypeId:int}/status")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> UpdateStatus(
        int businessTypeId,
        [FromBody] UpdateBusinessTypeStatusRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var result = await _businessTypes.UpdateStatusAsync(businessTypeId, request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] BusinessTypeQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var result = await _businessTypes.GetAllAsync(query, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("{businessTypeId:int}")]
    public async Task<IActionResult> GetById(
        int businessTypeId,
        CancellationToken cancellationToken = default)
    {
        var result = await _businessTypes.GetByIdAsync(businessTypeId, cancellationToken);
        return ToActionResult(result);
    }
}
