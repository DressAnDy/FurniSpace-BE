#nullable enable

using System.Security.Claims;
using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.Products;
using FurniSpace.Application.Interfaces.Products;
using FurniSpace.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers;

[Route("products")]
public sealed class ProductsController : BaseApiController
{
    private const long MultipartRequestLimitBytes = 100L * 1024L * 1024L;

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

    [Authorize(Roles = "ADMIN")]
    [HttpPatch("{productId:guid}")]
    public async Task<IActionResult> Update(
        Guid productId,
        [FromBody] UpdateProductRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var result = await _products.UpdateAsync(productId, request, cancellationToken);
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

    [AllowAnonymous]
    [HttpPost("{productId:guid}/files")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MultipartRequestLimitBytes)]
    public async Task<IActionResult> UploadFile(
        Guid productId,
        [FromForm] UploadCatalogFileFormRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _products.UploadFileAsync(
            productId,
            currentUserId,
            new UploadCatalogFileRequestDto
            {
                Content = request.File?.OpenReadStream() ?? Stream.Null,
                OriginalFileName = request.File?.FileName ?? string.Empty,
                ContentType = request.File?.ContentType ?? "application/octet-stream",
                FileSizeBytes = request.File?.Length ?? 0,
                FileType = request.FileType,
                Visibility = request.Visibility,
                Description = request.Description
            },
            cancellationToken);

        return ToActionResult(result);
    }

    private bool TryGetCurrentUserId(out Guid currentUserId)
    {
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out currentUserId);
    }
}

public sealed class UploadCatalogFileFormRequest
{
    public IFormFile? File { get; set; }
    public FileType FileType { get; set; } = FileType.OTHER;
    public FileVisibility? Visibility { get; set; }
    public string? Description { get; set; }
}
