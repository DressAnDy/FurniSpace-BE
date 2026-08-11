#nullable enable

using System.Security.Claims;
using FurniSpace.API.Base;
using FurniSpace.API.DTOs.Products;
using FurniSpace.Application.DTOs.Catalog;
using FurniSpace.Application.DTOs.ProductVersions;
using FurniSpace.Application.Interfaces.ProductVersions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers.Catalog;

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

    [Authorize(Roles = "DESIGNER,ADMIN")]
    [HttpPost("products/{productId:guid}/versions")]
    public async Task<IActionResult> Create(
        Guid productId,
        [FromBody] CreateProductVersionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var result = await _productVersions.CreateAsync(
            productId,
            request,
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "ADMIN")]
    [HttpGet("products/{productId:guid}/versions")]
    public async Task<IActionResult> GetListByProduct(
        Guid productId,
        [FromQuery] ProductVersionListQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var result = await _productVersions.GetListByProductAsync(productId, query, cancellationToken);
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
    [HttpPatch("product-versions/{productVersionId:guid}/activate")]
    public async Task<IActionResult> Activate(
        Guid productVersionId,
        CancellationToken cancellationToken = default)
    {
        var result = await _productVersions.ActivateAsync(productVersionId, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "ADMIN")]
    [HttpPatch("product-versions/{productVersionId:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(
        Guid productVersionId,
        CancellationToken cancellationToken = default)
    {
        var result = await _productVersions.DeactivateAsync(productVersionId, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "ADMIN")]
    [HttpPatch("product-versions/{productVersionId:guid}/archive")]
    public async Task<IActionResult> Archive(
        Guid productVersionId,
        CancellationToken cancellationToken = default)
    {
        var result = await _productVersions.ArchiveAsync(productVersionId, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "ADMIN")]
    [HttpPatch("product-versions/{productVersionId:guid}/restore")]
    public async Task<IActionResult> Restore(
        Guid productVersionId,
        CancellationToken cancellationToken = default)
    {
        var result = await _productVersions.RestoreAsync(productVersionId, cancellationToken);
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
            request.ToRequestDto(),
            cancellationToken);

        return ToActionResult(result);
    }

    private bool TryGetCurrentUserId(out Guid currentUserId)
    {
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out currentUserId);
    }
}
