#nullable enable

using System.Security.Claims;
using FurniSpace.API.Base;
using FurniSpace.API.DTOs.Products;
using FurniSpace.Application.DTOs.LayoutAssets;
using FurniSpace.Application.Interfaces.LayoutAssets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers.Catalog;

[Route("layout-assets")]
public sealed class LayoutAssetsController : BaseApiController
{
    private const long MultipartRequestLimitBytes = 100L * 1024L * 1024L;

    private readonly ILayoutAssetService _layoutAssets;

    public LayoutAssetsController(ILayoutAssetService layoutAssets)
    {
        _layoutAssets = layoutAssets;
    }

    [Authorize(Roles = "ADMIN")]
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateLayoutAssetRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _layoutAssets.CreateAsync(request, currentUserId, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "ADMIN")]
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] LayoutAssetQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var result = await _layoutAssets.GetAllAsync(query, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "ADMIN,DESIGNER")]
    [HttpGet("{layoutAssetId:guid}")]
    public async Task<IActionResult> GetById(
        Guid layoutAssetId,
        CancellationToken cancellationToken = default)
    {
        var roleName = User.FindFirstValue(ClaimTypes.Role);
        var result = await _layoutAssets.GetByIdAsync(layoutAssetId, roleName, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "ADMIN")]
    [HttpPatch("{layoutAssetId:guid}")]
    public async Task<IActionResult> Update(
        Guid layoutAssetId,
        [FromBody] UpdateLayoutAssetRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var result = await _layoutAssets.UpdateAsync(layoutAssetId, request, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "ADMIN")]
    [HttpPatch("{layoutAssetId:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid layoutAssetId,
        [FromBody] UpdateLayoutAssetStatusRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var result = await _layoutAssets.UpdateStatusAsync(layoutAssetId, request, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "ADMIN")]
    [HttpPost("{layoutAssetId:guid}/files")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MultipartRequestLimitBytes)]
    public async Task<IActionResult> UploadFile(
        Guid layoutAssetId,
        [FromForm] UploadCatalogFileFormRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _layoutAssets.UploadFileAsync(
            layoutAssetId,
            currentUserId,
            request.ToRequestDto(),
            cancellationToken);

        return ToActionResult(result);
    }

    [Authorize(Roles = "ADMIN")]
    [HttpGet("{layoutAssetId:guid}/files")]
    public async Task<IActionResult> GetFiles(
        Guid layoutAssetId,
        CancellationToken cancellationToken = default)
    {
        var result = await _layoutAssets.GetFilesAsync(layoutAssetId, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "ADMIN")]
    [HttpPatch("{layoutAssetId:guid}/files/{fileId:guid}/primary")]
    public async Task<IActionResult> SetPrimaryFile(
        Guid layoutAssetId,
        Guid fileId,
        CancellationToken cancellationToken = default)
    {
        var result = await _layoutAssets.SetPrimaryFileAsync(layoutAssetId, fileId, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Roles = "ADMIN")]
    [HttpDelete("{layoutAssetId:guid}/files/{fileId:guid}")]
    public async Task<IActionResult> DeleteFile(
        Guid layoutAssetId,
        Guid fileId,
        CancellationToken cancellationToken = default)
    {
        var result = await _layoutAssets.DeleteFileAsync(layoutAssetId, fileId, cancellationToken);
        return ToActionResult(result);
    }

    private bool TryGetCurrentUserId(out Guid currentUserId)
    {
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out currentUserId);
    }
}
