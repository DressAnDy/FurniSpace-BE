#nullable enable

using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.Categories;
using FurniSpace.Application.Interfaces.Categories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers.Catalog;

[Route("categories")]
public sealed class CategoriesController : BaseApiController
{
    private readonly ICategoryService _categories;

    public CategoriesController(ICategoryService categories)
    {
        _categories = categories;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _categories.GetAllAsync(page, limit, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "ADMIN")]
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateCategoryRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var result = await _categories.CreateAsync(request, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "ADMIN")]
    [HttpPut("{categoryId:guid}")]
    public async Task<IActionResult> Update(
        Guid categoryId,
        [FromBody] UpdateCategoryRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var result = await _categories.UpdateAsync(categoryId, request, cancellationToken);
        return ToActionResult(result);
    }
}
