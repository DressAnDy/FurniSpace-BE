#nullable enable

using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.Products;
using FurniSpace.Application.Interfaces.Products;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers;

[Route("products")]
public sealed class ProductsController : BaseApiController
{
    private readonly IProductService _products;

    public ProductsController(IProductService products)
    {
        _products = products;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _products.GetAllAsync(page, limit, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "ADMIN")]
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateProductRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var result = await _products.CreateAsync(request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("{productId:guid}")]
    public async Task<IActionResult> GetById(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        var result = await _products.GetByIdAsync(productId, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("by-category/{categoryId:guid}")]
    public async Task<IActionResult> GetByCategory(
        Guid categoryId,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        [FromQuery] bool includeDefaultVersion = true,
        CancellationToken cancellationToken = default)
    {
        var result = await _products.GetByCategoryAsync(
            categoryId,
            page,
            limit,
            includeDefaultVersion,
            cancellationToken);
        return ToActionResult(result);
    }
}
