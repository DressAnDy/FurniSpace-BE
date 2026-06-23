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
    private const long PreviewMultipartRequestLimitBytes = 10L * 1024L * 1024L;

    private readonly IProductService _products;
    private readonly IProductPreviewImageService _previewImages;

    public ProductsController(IProductService products, IProductPreviewImageService previewImages)
    {
        _products = products;
        _previewImages = previewImages;
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

    [Authorize(Roles = "ADMIN")]
    [HttpPost("{productId:guid}/files")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MultipartRequestLimitBytes)]
    public async Task<IActionResult> UploadFile(
        Guid productId,
        [FromForm] UploadCatalogFileFormRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
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
                Description = request.Description,
                DisplayOrder = request.DisplayOrder
            },
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpGet("{productId:guid}/preview-files")]
    public async Task<IActionResult> GetPreviewFiles(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        var result = await _previewImages.GetListAsync(productId, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "ADMIN")]
    [HttpPost("{productId:guid}/preview-files")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(PreviewMultipartRequestLimitBytes)]
    public async Task<IActionResult> UploadPreviewFile(
        Guid productId,
        [FromForm] UploadProductPreviewImageFormRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _previewImages.UploadAsync(
            productId,
            currentUserId,
            new UploadProductPreviewImageRequestDto
            {
                Content = request.File?.OpenReadStream() ?? Stream.Null,
                OriginalFileName = request.File?.FileName ?? string.Empty,
                ContentType = request.File?.ContentType ?? "application/octet-stream",
                FileSizeBytes = request.File?.Length ?? 0,
                Description = request.Description,
                DisplayOrder = request.DisplayOrder
            },
            cancellationToken);

        return ToActionResult(result);
    }
}

public sealed class UploadCatalogFileFormRequest
{
    public IFormFile? File { get; set; }
    public FileType FileType { get; set; } = FileType.OTHER;
    public FileVisibility? Visibility { get; set; }
    public string? Description { get; set; }
    public int? DisplayOrder { get; set; }
}

public sealed class UploadProductPreviewImageFormRequest
{
    public IFormFile? File { get; set; }
    public string? Description { get; set; }
    public int? DisplayOrder { get; set; }
}
