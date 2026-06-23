#nullable enable

using System.Security.Claims;
using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.ProductVersions;
using FurniSpace.Application.DTOs.Products;
using FurniSpace.Application.Interfaces.ProductVersions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers;

public sealed class ProductVersionsController : BaseApiController
{
    private const long MultipartRequestLimitBytes = 100L * 1024L * 1024L;

    private readonly IProductVersionService _productVersions;

    public ProductVersionsController(IProductVersionService productVersions)
    {
        _productVersions = productVersions;
    }

    [HttpGet("product-versions/{productVersionId:guid}")]
    public async Task<IActionResult> GetById(
        Guid productVersionId,
        CancellationToken cancellationToken = default)
    {
        var result = await _productVersions.GetByIdAsync(productVersionId, cancellationToken);
        return ToActionResult(result);
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

    [Authorize(Roles = "ADMIN")]
    [HttpPost("product-versions/{productVersionId:guid}/files")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MultipartRequestLimitBytes)]
    public async Task<IActionResult> UploadFile(
        Guid productVersionId,
        [FromForm] UploadCatalogFileFormRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _productVersions.UploadFileAsync(
            productVersionId,
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

    private bool TryGetCurrentUserId(out Guid currentUserId)
    {
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out currentUserId);
    }
}
