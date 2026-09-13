#nullable enable

using System.Security.Claims;
using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.Common;
using FurniSpace.Application.DTOs.Products;
using FurniSpace.Application.Interfaces.Products;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers.Catalog;

[Route("products")]
public sealed class ProductsController : BaseApiController
{
    private readonly IProductService _products;
    private readonly IProductPreviewImageService _previewImages;

    public ProductsController(IProductService products, IProductPreviewImageService previewImages)
    {
        _products = products;
        _previewImages = previewImages;
    }

    [HttpGet("suggest")]
    public async Task<IActionResult> Suggest(
        [FromQuery] string q,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await _products.SuggestAsync(q, limit, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] ProductSearchRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var result = await _products.SearchAsync(request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        [FromQuery] int[]? businessTypeIds = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _products.GetAllAsync(page, limit, businessTypeIds, cancellationToken);
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

    [HttpGet("{productId:guid}/similar")]
    public async Task<IActionResult> GetSimilar(
        Guid productId,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await _products.GetSimilarAsync(productId, limit, cancellationToken);
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
    [HttpPatch("{productId:guid}/activate")]
    public async Task<IActionResult> Activate(Guid productId, CancellationToken cancellationToken = default)
    {
        var result = await _products.ActivateAsync(productId, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "ADMIN")]
    [HttpPatch("{productId:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid productId, CancellationToken cancellationToken = default)
    {
        var result = await _products.DeactivateAsync(productId, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "ADMIN")]
    [HttpPatch("{productId:guid}/archive")]
    public async Task<IActionResult> Archive(Guid productId, CancellationToken cancellationToken = default)
    {
        var result = await _products.ArchiveAsync(productId, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "ADMIN")]
    [HttpPatch("{productId:guid}/restore")]
    public async Task<IActionResult> Restore(Guid productId, CancellationToken cancellationToken = default)
    {
        var result = await _products.RestoreAsync(productId, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "ADMIN")]
    [HttpPost("{productId:guid}/files/upload-url")]
    public async Task<IActionResult> PrepareFileUpload(
        Guid productId,
        [FromBody] UploadCatalogFileRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _products.PrepareFileUploadAsync(
            productId,
            currentUserId,
            request,
            cancellationToken);

        return ToActionResult(result);
    }

    [Authorize(Roles = "ADMIN")]
    [HttpPost("{productId:guid}/files/complete")]
    public async Task<IActionResult> CompleteFileUpload(
        Guid productId,
        [FromBody] CompleteDirectUploadRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _products.CompleteFileUploadAsync(
            productId,
            currentUserId,
            request,
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
    [HttpPost("{productId:guid}/preview-files/upload-url")]
    public async Task<IActionResult> PreparePreviewFileUpload(
        Guid productId,
        [FromBody] UploadProductPreviewImageRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _previewImages.PreparePreviewUploadAsync(
            productId,
            currentUserId,
            request,
            cancellationToken);

        return ToActionResult(result);
    }

    [Authorize(Roles = "ADMIN")]
    [HttpPost("{productId:guid}/preview-files/complete")]
    public async Task<IActionResult> CompletePreviewFileUpload(
        Guid productId,
        [FromBody] CompleteDirectUploadRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _previewImages.CompletePreviewUploadAsync(
            productId,
            currentUserId,
            request,
            cancellationToken);

        return ToActionResult(result);
    }
}
