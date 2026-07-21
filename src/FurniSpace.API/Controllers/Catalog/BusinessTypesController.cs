using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.BusinessTypes;
using FurniSpace.Application.Interfaces.BusinessTypes;
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
