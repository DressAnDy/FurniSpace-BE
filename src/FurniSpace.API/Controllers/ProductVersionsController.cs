#nullable enable

using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.ProductVersions;
using FurniSpace.Application.Interfaces.ProductVersions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers;

public sealed class ProductVersionsController : BaseApiController
{
    private readonly IProductVersionService _productVersions;

    public ProductVersionsController(IProductVersionService productVersions)
    {
        _productVersions = productVersions;
    }

    [Authorize(Roles = "ADMIN")]
    [HttpPost("products/{productId:guid}/versions")]
    public async Task<IActionResult> Create(
        Guid productId,
        [FromBody] CreateProductVersionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var result = await _productVersions.CreateAsync(productId, request, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "ADMIN")]
    [HttpPatch("product-versions/{productVersionId:guid}")]
    public async Task<IActionResult> Update(
        Guid productVersionId,
        [FromBody] UpdateProductVersionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var result = await _productVersions.UpdateAsync(productVersionId, request, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "ADMIN")]
    [HttpPatch("product-versions/{productVersionId:guid}/set-default")]
    public async Task<IActionResult> SetDefault(
        Guid productVersionId,
        CancellationToken cancellationToken = default)
    {
        var result = await _productVersions.SetDefaultAsync(productVersionId, cancellationToken);
        return ToActionResult(result);
    }
}
